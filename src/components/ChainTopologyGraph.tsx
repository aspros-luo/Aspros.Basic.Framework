import React, { useState } from 'react';
import {
  Server,
  Zap,
  Database,
  CheckCircle2,
  AlertTriangle,
  Clock,
  ArrowRight,
  Shield,
  Layers,
  Terminal,
  Activity
} from 'lucide-react';
import type { RpcHopTrace, RpcTraceResult } from '../types';

interface ChainTopologyGraphProps {
  chainResult: RpcTraceResult | null;
  defaultHops?: Array<{
    service: string;
    protocol: 'HTTP/REST' | 'gRPC/HTTP2' | 'Internal-IPC';
    action: string;
    role: string;
  }>;
}

export function ChainTopologyGraph({ chainResult, defaultHops }: ChainTopologyGraphProps) {
  const [selectedNodeIndex, setSelectedNodeIndex] = useState<number | null>(null);

  // If chainResult exists, use its hops; otherwise use default preview hops
  const hops: Array<Partial<RpcHopTrace> & { role?: string }> = chainResult?.hops?.length
    ? chainResult.hops
    : defaultHops || [
        {
          service: 'api-gateway',
          protocol: 'HTTP/REST',
          durationMs: 4,
          status: 'OK',
          action: '入口路由校验 & 速率检查',
          headersPassed: { 'x-trace-id': 'trace-preview', 'x-tenant-id': '100' },
          role: 'API 网关',
        },
        {
          service: 'framework-core-consumer',
          protocol: 'HTTP/REST',
          durationMs: 12,
          status: 'OK',
          action: 'CoreController 承接并转发 Token',
          headersPassed: { 'x-trace-id': 'trace-preview', Authorization: 'Bearer aspros-jwt...' },
          role: 'Web API Consumer',
        },
        {
          service: 'framework-core-provider',
          protocol: 'gRPC/HTTP2',
          durationMs: 18,
          status: 'OK',
          action: 'CoreGrpcService.PingAsync',
          headersPassed: { 'grpc-timeout': '500m', 'x-forwarded-user': '10001' },
          role: 'gRPC Provider',
        },
        {
          service: 'payment.service',
          protocol: 'Internal-IPC',
          durationMs: 9,
          status: 'OK',
          action: '多租户结算状态检验',
          headersPassed: { 'x-tenant-id': '100' },
          role: '下游结算服务',
        },
      ];

  const nodeCount = hops.length;
  // Node geometry in SVG coordinates
  const svgWidth = 960;
  const svgHeight = 220;
  const startX = 90;
  const endX = svgWidth - 90;
  const stepX = nodeCount > 1 ? (endX - startX) / (nodeCount - 1) : 0;
  const nodeY = 95;

  const getServiceIcon = (service: string) => {
    if (service.includes('gateway')) return <Shield className="w-4 h-4 text-blue-400" />;
    if (service.includes('consumer')) return <Zap className="w-4 h-4 text-purple-400" />;
    if (service.includes('provider')) return <Layers className="w-4 h-4 text-emerald-400" />;
    return <Database className="w-4 h-4 text-amber-400" />;
  };

  const getStatusColor = (status?: string) => {
    if (status === 'OK') return '#10b981'; // emerald-500
    if (status === 'TIMEOUT') return '#f43f5e'; // rose-500
    if (status === 'FAILED') return '#ef4444'; // red-500
    return '#3b82f6'; // blue-500
  };

  const selectedHop = selectedNodeIndex !== null ? hops[selectedNodeIndex] : null;

  return (
    <div className="bg-slate-950/80 border border-slate-800 rounded-xl p-5 mt-6 shadow-inner relative overflow-hidden">
      {/* Topology Header & Legend */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800/80 text-xs">
        <div className="flex items-center gap-2">
          <Activity className="w-4 h-4 text-blue-400" />
          <span className="font-semibold text-slate-200">
            动态服务拓扑流转图 (Live Request DAG Topology)
          </span>
          <span className="text-[10px] bg-slate-800 px-2 py-0.5 rounded text-slate-400 font-mono">
            {chainResult ? `${chainResult.hops.length} 级跳步` : '就绪待调用'}
          </span>
        </div>

        <div className="flex items-center gap-3 text-[11px] text-slate-400 flex-wrap">
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500"></span>
            <span>正常 (200 OK)</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-rose-500 animate-pulse"></span>
            <span>超时中断 (504 Timeout)</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-4 h-0.5 bg-blue-500"></span>
            <span>请求流向 (Animated Flow)</span>
          </div>
        </div>
      </div>

      {/* SVG Canvas */}
      <div className="w-full overflow-x-auto scrollbar-thin py-2">
        <div className="min-w-[800px]">
          <svg
            viewBox={`0 0 ${svgWidth} ${svgHeight}`}
            className="w-full h-auto select-none overflow-visible"
            style={{ minHeight: '190px' }}
          >
            <defs>
              {/* Arrow Marker Definitions */}
              <marker
                id="arrow-success"
                viewBox="0 0 10 10"
                refX="8"
                refY="5"
                markerWidth="6"
                markerHeight="6"
                orient="auto-start-reverse"
              >
                <path d="M 0 1.5 L 8 5 L 0 8.5 z" fill="#10b981" />
              </marker>

              <marker
                id="arrow-fail"
                viewBox="0 0 10 10"
                refX="8"
                refY="5"
                markerWidth="6"
                markerHeight="6"
                orient="auto-start-reverse"
              >
                <path d="M 0 1.5 L 8 5 L 0 8.5 z" fill="#f43f5e" />
              </marker>

              <marker
                id="arrow-blue"
                viewBox="0 0 10 10"
                refX="8"
                refY="5"
                markerWidth="6"
                markerHeight="6"
                orient="auto-start-reverse"
              >
                <path d="M 0 1.5 L 8 5 L 0 8.5 z" fill="#3b82f6" />
              </marker>

              {/* Linear Gradients for Link Paths */}
              <linearGradient id="flowGradient" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stopColor="#3b82f6" stopOpacity="0.8" />
                <stop offset="50%" stopColor="#8b5cf6" stopOpacity="0.9" />
                <stop offset="100%" stopColor="#10b981" stopOpacity="0.8" />
              </linearGradient>

              {/* Glow Filter */}
              <filter id="nodeGlow" x="-20%" y="-20%" width="140%" height="140%">
                <feGaussianBlur stdDeviation="3" result="blur" />
                <feComposite in="SourceGraphic" in2="blur" operator="over" />
              </filter>

              <style>{`
                @keyframes dashFlow {
                  to {
                    stroke-dashoffset: -28;
                  }
                }
                .flow-animated {
                  stroke-dasharray: 6, 6;
                  animation: dashFlow 1.2s linear infinite;
                }
                .pulse-ring {
                  animation: pulseRing 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
                }
                @keyframes pulseRing {
                  0%, 100% { opacity: 0.2; transform: scale(1); }
                  50% { opacity: 0.6; transform: scale(1.15); }
                }
              `}</style>
            </defs>

            {/* Connecting Edges / Paths */}
            {hops.map((hop, index) => {
              if (index === hops.length - 1) return null;
              const nextHop = hops[index + 1];
              const fromX = startX + index * stepX + 50;
              const toX = startX + (index + 1) * stepX - 50;
              const midX = (fromX + toX) / 2;

              const isFailedLink = nextHop.status === 'TIMEOUT' || nextHop.status === 'FAILED';
              const strokeColor = isFailedLink ? '#f43f5e' : getStatusColor(hop.status);
              const markerId = isFailedLink ? 'url(#arrow-fail)' : 'url(#arrow-success)';

              return (
                <g key={`edge-${index}`}>
                  {/* Background Track Line */}
                  <line
                    x1={fromX}
                    y1={nodeY}
                    x2={toX}
                    y2={nodeY}
                    stroke="#1e293b"
                    strokeWidth="4"
                    strokeLinecap="round"
                  />

                  {/* Animated Flow Line */}
                  <line
                    x1={fromX}
                    y1={nodeY}
                    x2={toX}
                    y2={nodeY}
                    stroke={strokeColor}
                    strokeWidth="2.5"
                    className="flow-animated"
                    markerEnd={markerId}
                  />

                  {/* Protocol & Duration Pill Badge */}
                  <g transform={`translate(${midX}, ${nodeY - 14})`}>
                    <rect
                      x="-42"
                      y="-12"
                      width="84"
                      height="20"
                      rx="10"
                      fill="#090d16"
                      stroke={isFailedLink ? '#f43f5e' : '#334155'}
                      strokeWidth="1"
                    />
                    <text
                      textAnchor="middle"
                      y="2"
                      fill={isFailedLink ? '#fda4af' : '#94a3b8'}
                      fontSize="9.5"
                      fontFamily="monospace"
                      fontWeight="600"
                    >
                      {nextHop.protocol || 'RPC'}
                    </text>
                  </g>
                </g>
              );
            })}

            {/* Nodes */}
            {hops.map((hop, index) => {
              const cx = startX + index * stepX;
              const cy = nodeY;
              const isSelected = selectedNodeIndex === index;
              const isFailed = hop.status === 'TIMEOUT' || hop.status === 'FAILED';
              const nodeColor = isFailed ? '#f43f5e' : getStatusColor(hop.status);

              return (
                <g
                  key={`node-${index}`}
                  transform={`translate(${cx}, ${cy})`}
                  onClick={() => setSelectedNodeIndex(isSelected ? null : index)}
                  className="cursor-pointer group"
                >
                  {/* Selection / Pulse Halo */}
                  {(isSelected || isFailed) && (
                    <circle
                      r="40"
                      fill={nodeColor}
                      opacity={isFailed ? '0.25' : '0.15'}
                      className={isFailed ? 'pulse-ring' : ''}
                    />
                  )}

                  {/* Main Node Box */}
                  <rect
                    x="-46"
                    y="-34"
                    width="92"
                    height="68"
                    rx="12"
                    fill={isSelected ? '#1e293b' : '#0f172a'}
                    stroke={isSelected ? '#60a5fa' : nodeColor}
                    strokeWidth={isSelected ? '2.5' : '1.5'}
                    filter="url(#nodeGlow)"
                    className="transition-all duration-200 group-hover:stroke-blue-400"
                  />

                  {/* Top Step Number Badge */}
                  <g transform="translate(0, -34)">
                    <rect
                      x="-14"
                      y="-7"
                      width="28"
                      height="14"
                      rx="7"
                      fill="#020617"
                      stroke={nodeColor}
                      strokeWidth="1"
                    />
                    <text
                      textAnchor="middle"
                      y="3.5"
                      fill="#e2e8f0"
                      fontSize="9"
                      fontWeight="bold"
                      fontFamily="monospace"
                    >
                      #{index + 1}
                    </text>
                  </g>

                  {/* Node Title Text (Shortened) */}
                  <text
                    textAnchor="middle"
                    y="-8"
                    fill="#f8fafc"
                    fontSize="11"
                    fontWeight="600"
                    fontFamily="sans-serif"
                  >
                    {hop.service?.replace('framework-core-', '').replace('.service', '')}
                  </text>

                  {/* Protocol Subtitle */}
                  <text
                    textAnchor="middle"
                    y="7"
                    fill="#64748b"
                    fontSize="9.5"
                    fontFamily="monospace"
                  >
                    {hop.protocol}
                  </text>

                  {/* Duration Badge inside Node */}
                  <g transform="translate(0, 20)">
                    <text
                      textAnchor="middle"
                      fill={isFailed ? '#f43f5e' : '#10b981'}
                      fontSize="10"
                      fontWeight="bold"
                      fontFamily="monospace"
                    >
                      {hop.durationMs !== undefined ? `${hop.durationMs}ms` : 'Ready'}
                    </text>
                  </g>

                  {/* Bottom Action / Role Label */}
                  <text
                    textAnchor="middle"
                    y="50"
                    fill="#94a3b8"
                    fontSize="10.5"
                    fontFamily="sans-serif"
                    className="group-hover:fill-slate-200"
                  >
                    {hop.service}
                  </text>

                  {/* Status Indicator Icon */}
                  <g transform="translate(32, -22)">
                    <circle r="7" fill="#020617" />
                    {hop.status === 'OK' ? (
                      <circle r="5" fill="#10b981" />
                    ) : (
                      <circle r="5" fill="#f43f5e" />
                    )}
                  </g>
                </g>
              );
            })}
          </svg>
        </div>
      </div>

      {/* Selected Node Details Drawer */}
      {selectedHop && (
        <div className="mt-4 p-3.5 bg-slate-900 border border-slate-800 rounded-lg text-xs space-y-2 animate-in fade-in duration-150">
          <div className="flex items-center justify-between pb-2 border-b border-slate-800">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-blue-400"></span>
              <span className="font-semibold text-slate-200">
                节点详细信息: <code className="text-blue-300 font-mono">{selectedHop.service}</code>
              </span>
              <span className="text-[10px] bg-slate-800 px-1.5 py-0.5 rounded text-slate-400 font-mono">
                {selectedHop.protocol}
              </span>
            </div>
            <div className="flex items-center gap-2 font-mono text-[11px]">
              <span className="text-slate-400">耗时:</span>
              <span className={selectedHop.status === 'OK' ? 'text-emerald-400 font-bold' : 'text-rose-400 font-bold'}>
                {selectedHop.durationMs}ms [{selectedHop.status}]
              </span>
              <button
                onClick={() => setSelectedNodeIndex(null)}
                className="ml-2 text-slate-500 hover:text-slate-300 text-xs"
              >
                ✕ 关闭
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-slate-300">
            <div>
              <span className="text-[11px] text-slate-400 block mb-0.5 font-medium">执行动作 (Action):</span>
              <div className="p-2 bg-slate-950 rounded border border-slate-800/80 font-mono text-[11px] text-slate-200">
                {selectedHop.action || '标准微服务调用处理'}
              </div>
            </div>

            <div>
              <span className="text-[11px] text-slate-400 block mb-0.5 font-medium">
                透传的 HTTP/gRPC 上下文标头 (Propagated Headers):
              </span>
              <pre className="p-2 bg-slate-950 rounded border border-slate-800/80 font-mono text-[11px] text-slate-300 overflow-x-auto">
                {JSON.stringify(selectedHop.headersPassed || {}, null, 2)}
              </pre>
            </div>
          </div>
        </div>
      )}

      {/* Optimization Gain Badges */}
      {chainResult?.optimizations && (
        <div className="mt-3 p-2.5 bg-slate-900/90 border border-slate-800 rounded-lg flex flex-wrap items-center justify-between gap-2 text-xs font-mono">
          <div className="flex items-center gap-2 flex-wrap">
            {chainResult.optimizations.zoneAffinityApplied && (
              <span className="flex items-center gap-1.5 px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                <span>⚡ 就近路由生效: 同机房直连 (节省 ~{chainResult.optimizations.latencySavedMs}ms)</span>
              </span>
            )}
            {chainResult.optimizations.idempotencyApplied && (
              <span className="flex items-center gap-1.5 px-2 py-0.5 rounded bg-amber-500/10 text-amber-300 border border-amber-500/30">
                <span>🛡 幂等防重切面生效 [{chainResult.optimizations.idempotencyKey}]</span>
              </span>
            )}
            {chainResult.optimizations.scopeContextApplied && (
              <span className="flex items-center gap-1.5 px-2 py-0.5 rounded bg-indigo-500/10 text-indigo-300 border border-indigo-500/30">
                <span>📦 WorkContext Scope 异步隔离生效</span>
              </span>
            )}
          </div>
          <span className="text-[11px] text-slate-400">
            全链路总延时: <strong className="text-slate-100 font-bold">{chainResult.overallDurationMs} ms</strong>
          </span>
        </div>
      )}

      <div className="mt-2 text-[11px] text-slate-500 flex items-center justify-between">
        <span>💡 提示：点击任意服务节点矩形卡片可查看该节点在 Aspros 链路中接收与透传的完整请求头与执行动作。</span>
      </div>
    </div>
  );
}
