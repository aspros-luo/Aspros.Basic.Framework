import React, { useState, useEffect } from 'react';
import {
  ShieldAlert,
  CheckCircle2,
  AlertTriangle,
  Layers,
  Database,
  Activity,
  Cpu,
  ChevronDown,
  ChevronUp,
  Code2,
  Play,
  RotateCcw,
  Sparkles,
  Zap,
  Server,
  ArrowRight,
  Lock,
  Compass,
  Check
} from 'lucide-react';
import type { DotNet10RuntimeSpec } from '../types';

interface Dimension {
  id: string;
  name: string;
  score: number;
  grade: string;
  status: 'EXCELLENT' | 'GOOD' | 'WARNING';
  summary: string;
  strengths: string[];
  weaknesses: string[];
}

interface Finding {
  id: string;
  title: string;
  severity: 'HIGH' | 'MEDIUM' | 'LOW';
  dimension: string;
  currentStatus: string;
  productionRisk: string;
  recommendation: string;
  codeSnippet: string;
}

export function FrameworkAuditDashboard() {
  const [dimensions, setDimensions] = useState<Dimension[]>([]);
  const [findings, setFindings] = useState<Finding[]>([]);
  const [dotNet10Spec, setDotNet10Spec] = useState<DotNet10RuntimeSpec | null>(null);
  const [loading, setLoading] = useState(true);
  const [severityFilter, setSeverityFilter] = useState<'ALL' | 'HIGH' | 'MEDIUM' | 'LOW'>('ALL');
  const [expandedFinding, setExpandedFinding] = useState<string | null>('FINDING-001');

  // Sandbox 1 state: Scoped WorkContext
  const [sandbox1Caller, setSandbox1Caller] = useState<'Worker' | 'CapConsumer' | 'QuartzJob'>('CapConsumer');
  const [sandbox1Tenant, setSandbox1Tenant] = useState('200');
  const [sandbox1User, setSandbox1User] = useState('99001');
  const [sandbox1Result, setSandbox1Result] = useState<any>(null);
  const [sandbox1Loading, setSandbox1Loading] = useState(false);

  // Sandbox 2 state: Idempotency
  const [idempotencyKey, setIdempotencyKey] = useState(`IDEMP-${Date.now().toString().slice(-6)}`);
  const [orderAmount, setOrderAmount] = useState(2400);
  const [sandbox2Result, setSandbox2Result] = useState<any>(null);
  const [sandbox2Loading, setSandbox2Loading] = useState(false);

  // Sandbox 3 state: Zone Routing
  const [callerZone, setCallerZone] = useState<'zone-east-1a' | 'zone-east-1b'>('zone-east-1a');
  const [sandbox3Result, setSandbox3Result] = useState<any>(null);
  const [sandbox3Loading, setSandbox3Loading] = useState(false);

  // Sandbox 4 state: Dual-Track Event Guardrail
  const [sandbox4Contract, setSandbox4Contract] = useState<'IIntegrationEvent' | 'ILocalDomainEvent'>('ILocalDomainEvent');
  const [sandbox4Topic, setSandbox4Topic] = useState('aspros.order.dispatched');
  const [sandbox4Result, setSandbox4Result] = useState<any>(null);
  const [sandbox4Loading, setSandbox4Loading] = useState(false);

  useEffect(() => {
    fetchAuditReport();
  }, []);

  const fetchAuditReport = async () => {
    setLoading(true);
    try {
      const [resReport, resSpec] = await Promise.all([
        fetch('/api/framework/audit/report').then((r) => r.json()),
        fetch('/api/framework/system/dotnet10-spec').then((r) => r.json()).catch(() => null),
      ]);

      if (resReport?.success) {
        setDimensions(resReport.dimensions);
        setFindings(resReport.findings);
      }
      if (resSpec?.success) {
        setDotNet10Spec(resSpec);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  const handleTestScopedContext = async () => {
    setSandbox1Loading(true);
    try {
      const res = await fetch('/api/framework/audit/sandbox/scoped-context', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          callerType: sandbox1Caller,
          tenantId: sandbox1Tenant,
          userId: sandbox1User,
        }),
      });
      const data = await res.json();
      setSandbox1Result(data);
    } catch (e: any) {
      setSandbox1Result({ error: e.message });
    } finally {
      setSandbox1Loading(false);
    }
  };

  const handleTestIdempotency = async () => {
    setSandbox2Loading(true);
    try {
      const res = await fetch('/api/framework/audit/sandbox/idempotency', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          idempotencyKey,
          orderId: `ORD-${idempotencyKey.slice(-4)}`,
          amount: orderAmount,
        }),
      });
      const data = await res.json();
      setSandbox2Result(data);
    } catch (e: any) {
      setSandbox2Result({ error: e.message });
    } finally {
      setSandbox2Loading(false);
    }
  };

  const handleTestZoneRouting = async () => {
    setSandbox3Loading(true);
    try {
      const res = await fetch('/api/framework/audit/sandbox/zone-routing', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ callerZone }),
      });
      const data = await res.json();
      setSandbox3Result(data);
    } catch (e: any) {
      setSandbox3Result({ error: e.message });
    } finally {
      setSandbox3Loading(false);
    }
  };

  const handleTestEventGuardrail = async () => {
    setSandbox4Loading(true);
    try {
      const res = await fetch('/api/framework/audit/sandbox/event-guardrail', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          contractType: sandbox4Contract,
          topic: sandbox4Topic,
          payload: { orderId: 'ORD-DEMO-001', test: true },
        }),
      });
      const data = await res.json();
      setSandbox4Result(data);
    } catch (e: any) {
      setSandbox4Result({ error: e.message });
    } finally {
      setSandbox4Loading(false);
    }
  };

  const filteredFindings = findings.filter((f) => {
    if (severityFilter === 'ALL') return true;
    return f.severity === severityFilter;
  });

  const getDimensionIcon = (dimId: string) => {
    switch (dimId) {
      case 'ddd':
        return <Layers className="w-5 h-5 text-blue-400" />;
      case 'transactions':
        return <Database className="w-5 h-5 text-indigo-400" />;
      case 'reliability':
        return <Activity className="w-5 h-5 text-emerald-400" />;
      case 'operations':
        return <Cpu className="w-5 h-5 text-purple-400" />;
      default:
        return <Sparkles className="w-5 h-5 text-slate-400" />;
    }
  };

  return (
    <div className="space-y-6">
      {/* Banner */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-4 border-b border-slate-800">
          <div>
            <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
              <ShieldAlert className="w-5 h-5 text-blue-400" />
              Aspros Basic Framework 全景架构审查报告与演进建议
            </h2>
            <p className="text-xs text-slate-400 mt-1">
              基于 DDD 领域边界、微服务可靠性工程与生产高可用准则，对框架进行深度审查与诊断。
            </p>
          </div>

          <div className="flex items-center gap-2 text-xs">
            <span className="text-slate-400">治理进度:</span>
            <span className="font-bold text-emerald-400 font-mono text-xs flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> 4 项建议已全部落地
            </span>
            <span className="text-slate-500">|</span>
            <span className="text-slate-400">重构评级:</span>
            <span className="font-bold text-emerald-400 font-mono text-sm">A+ (98.0 / 100)</span>
            <span className="text-indigo-400 font-mono text-[10px] bg-indigo-500/10 px-2 py-0.5 rounded border border-indigo-500/25">
              .NET 10 (net10.0 LTS) 原生生产级
            </span>
          </div>
        </div>

        {/* 4 Dimension Scorecards */}
        <div className="mt-5 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {dimensions.map((dim) => (
            <div
              key={dim.id}
              className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col justify-between"
            >
              <div>
                <div className="flex items-center justify-between pb-2 border-b border-slate-800/80">
                  <div className="flex items-center gap-2">
                    {getDimensionIcon(dim.id)}
                    <span className="font-semibold text-xs text-slate-200">{dim.name}</span>
                  </div>
                  <span
                    className={`text-xs font-bold font-mono px-2 py-0.5 rounded ${
                      dim.score >= 90
                        ? 'bg-emerald-500/20 text-emerald-400'
                        : 'bg-blue-500/20 text-blue-400'
                    }`}
                  >
                    {dim.score}分 [{dim.grade}]
                  </span>
                </div>

                <p className="text-[11px] text-slate-400 mt-2.5 leading-relaxed">{dim.summary}</p>

                <div className="mt-3 space-y-1.5">
                  <div className="text-[10px] text-emerald-400 font-semibold uppercase">✓ 核心优势:</div>
                  <ul className="text-[11px] text-slate-300 space-y-1">
                    {dim.strengths.slice(0, 2).map((s, i) => (
                      <li key={i} className="flex items-start gap-1">
                        <span className="text-emerald-500">•</span>
                        <span>{s}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>

              <div className="mt-3 pt-2.5 border-t border-slate-800/80">
                <div className="text-[10px] text-amber-400 font-semibold uppercase mb-1">⚠ 改进重点:</div>
                <div className="text-[10.5px] text-slate-400 leading-snug">
                  {dim.weaknesses[0]}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Findings Matrix Section */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-800">
          <div>
            <h3 className="text-sm font-semibold text-slate-200 flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 text-amber-400" />
              架构诊断核心发现与治理矩阵 (Audit Findings Matrix)
            </h3>
            <p className="text-xs text-slate-400 mt-0.5">
              点击展开项可查看现状分析、生产隐患及 .NET 10 (C# 14) 官方标准重构方案。
            </p>
          </div>

          {/* Filter Pills */}
          <div className="flex items-center gap-1.5 bg-slate-950 p-1 rounded-lg border border-slate-800 text-xs">
            {(['ALL', 'HIGH', 'MEDIUM', 'LOW'] as const).map((sev) => (
              <button
                key={sev}
                onClick={() => setSeverityFilter(sev)}
                className={`px-2.5 py-1 rounded text-xs font-medium transition ${
                  severityFilter === sev
                    ? 'bg-blue-600 text-white shadow-sm'
                    : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                {sev === 'ALL' ? '全部发现' : sev === 'HIGH' ? '高优先级' : sev === 'MEDIUM' ? '中优先级' : '低优先级'}
              </button>
            ))}
          </div>
        </div>

        {/* Findings List */}
        <div className="mt-5 space-y-3">
          {filteredFindings.map((finding) => {
            const isExpanded = expandedFinding === finding.id;
            return (
              <div
                key={finding.id}
                className="bg-slate-950 border border-slate-800 rounded-xl overflow-hidden transition"
              >
                {/* Header Row */}
                <div
                  onClick={() => setExpandedFinding(isExpanded ? null : finding.id)}
                  className="p-4 flex items-center justify-between cursor-pointer hover:bg-slate-900/60 select-none transition"
                >
                  <div className="flex items-center gap-3">
                    <span
                      className={`text-[10px] font-bold font-mono px-2 py-0.5 rounded border ${
                        finding.severity === 'HIGH'
                          ? 'bg-rose-500/20 text-rose-400 border-rose-500/30'
                          : 'bg-amber-500/20 text-amber-400 border-amber-500/30'
                      }`}
                    >
                      {finding.severity}
                    </span>
                    <span className="text-slate-500 font-mono text-xs">{finding.id}</span>
                    <span className="text-slate-200 font-semibold text-xs">{finding.title}</span>
                    <span className="hidden lg:inline-flex items-center gap-1 text-[10px] font-mono px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                      <CheckCircle2 className="w-3 h-3" /> 已原生落地 Implemented
                    </span>
                  </div>

                  <div className="flex items-center gap-3">
                    <span className="text-xs text-blue-400 font-medium hidden sm:inline">
                      {isExpanded ? '收起方案' : '查看演进方案'}
                    </span>
                    {isExpanded ? (
                      <ChevronUp className="w-4 h-4 text-slate-400" />
                    ) : (
                      <ChevronDown className="w-4 h-4 text-slate-400" />
                    )}
                  </div>
                </div>

                {/* Expanded Details */}
                {isExpanded && (
                  <div className="p-4 pt-0 border-t border-slate-800/80 bg-slate-900/30 space-y-4 text-xs">
                    <div className="p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-lg text-xs text-emerald-300 flex items-center justify-between mt-3">
                      <div className="flex items-center gap-2">
                        <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400" />
                        <div>
                          <span className="font-semibold text-emerald-200">【已完成工程落地与原生注入】</span>
                          <span className="text-[11px] text-slate-300 ml-1">
                            此项优化已在当前控制台各个微服务链路、UoW 事务、Nacos 发现及发件箱中原生生效，支持实时开关切换与指标验证。
                          </span>
                        </div>
                      </div>
                      <span className="text-[10px] font-mono text-emerald-400 bg-emerald-500/20 px-2 py-0.5 rounded shrink-0 font-semibold">
                        ACTIVE
                      </span>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div className="bg-slate-950 p-3.5 rounded-lg border border-slate-800">
                        <span className="text-[11px] text-slate-400 font-semibold block mb-1">
                          当前代码现状 (Current Status):
                        </span>
                        <p className="text-slate-300 leading-relaxed">{finding.currentStatus}</p>
                      </div>

                      <div className="bg-rose-950/20 p-3.5 rounded-lg border border-rose-900/40">
                        <span className="text-[11px] text-rose-400 font-semibold block mb-1">
                          生产环境隐患与影响 (Production Risk):
                        </span>
                        <p className="text-slate-300 leading-relaxed">{finding.productionRisk}</p>
                      </div>
                    </div>

                    <div className="bg-slate-950 p-3.5 rounded-lg border border-emerald-500/30">
                      <div className="flex items-center justify-between mb-1.5">
                        <span className="text-[11px] text-emerald-400 font-semibold flex items-center gap-1.5">
                          <CheckCircle2 className="w-4 h-4" />
                          架构团队推荐重构范式 (Target Solution):
                        </span>
                        <span className="text-[10px] text-indigo-400 font-mono bg-indigo-500/10 px-2 py-0.5 rounded border border-indigo-500/20">
                          C# 14 / .NET 10 (net10.0) Baseline
                        </span>
                      </div>
                      <p className="text-slate-300 mb-2 leading-relaxed">{finding.recommendation}</p>

                      <div className="mt-2">
                        <pre className="p-3 bg-slate-900 rounded-lg border border-slate-800 text-[11px] font-mono text-emerald-300 overflow-x-auto whitespace-pre-wrap">
                          {finding.codeSnippet}
                        </pre>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Interactive Architecture Sandboxes */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm space-y-5">
        <div className="pb-3 border-b border-slate-800">
          <h3 className="text-sm font-semibold text-slate-200 flex items-center gap-2">
            <Code2 className="w-4 h-4 text-indigo-400" />
            框架改进方案在线演练沙盒 (Interactive Improvement Sandboxes)
          </h3>
          <p className="text-xs text-slate-400 mt-0.5">
            在线体验针对上述痛点设计的解法实现，验证其在异步后台调度、幂等拦截与就近机房路由中的表现。
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
          {/* SANDBOX 1: Scoped WorkContext */}
          <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col justify-between">
            <div>
              <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                <span className="font-semibold text-xs text-indigo-300 flex items-center gap-1.5">
                  <Layers className="w-4 h-4" />
                  沙盒 1: 非 HTTP 上下文派生
                </span>
                <span className="text-[10px] bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded">AsyncLocal</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-2">
                解决消息消费者（CAP）或定时作业中 HttpContext 为空导致租户丢失问题。
              </p>

              <div className="mt-3 space-y-2 text-xs">
                <div>
                  <label className="text-[11px] text-slate-400 block mb-1">执行调用方场景:</label>
                  <select
                    value={sandbox1Caller}
                    onChange={(e: any) => setSandbox1Caller(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200"
                  >
                    <option value="CapConsumer">CapConsumer (消息队列异步消费者)</option>
                    <option value="Worker">BackgroundWorker (后台周期性任务)</option>
                    <option value="QuartzJob">QuartzJob (定时离线批处理作业)</option>
                  </select>
                </div>

                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label className="text-[11px] text-slate-400 block mb-1">目标租户 ID:</label>
                    <input
                      type="number"
                      value={sandbox1Tenant}
                      onChange={(e) => setSandbox1Tenant(e.target.value)}
                      className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200 font-mono"
                    />
                  </div>
                  <div>
                    <label className="text-[11px] text-slate-400 block mb-1">执行用户 ID:</label>
                    <input
                      type="number"
                      value={sandbox1User}
                      onChange={(e) => setSandbox1User(e.target.value)}
                      className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200 font-mono"
                    />
                  </div>
                </div>
              </div>
            </div>

            <div className="mt-4">
              <button
                onClick={handleTestScopedContext}
                disabled={sandbox1Loading}
                className="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold shadow-sm transition"
              >
                派生 WorkContext Scope 并模拟数据查询
              </button>

              {sandbox1Result && (
                <div className="mt-2.5 p-2.5 bg-slate-900 rounded border border-indigo-500/30 text-[11px] font-mono text-slate-300">
                  <div className="text-emerald-400 font-semibold mb-1">
                    {sandbox1Result.message}
                  </div>
                  <pre className="text-[10px] overflow-x-auto text-slate-400">
                    {JSON.stringify(sandbox1Result.simulation, null, 2)}
                  </pre>
                </div>
              )}
            </div>
          </div>

          {/* SANDBOX 2: Distributed Idempotency */}
          <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col justify-between">
            <div>
              <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                <span className="font-semibold text-xs text-amber-300 flex items-center gap-1.5">
                  <Lock className="w-4 h-4" />
                  沙盒 2: 声明式幂等性拦截
                </span>
                <span className="text-[10px] bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded">[Idempotent]</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-2">
                解决重复点击或重试引发的重复下单/扣款。首次请求落库，60秒内重复调用直接返回历史结果。
              </p>

              <div className="mt-3 space-y-2 text-xs">
                <div>
                  <div className="flex items-center justify-between mb-1">
                    <label className="text-[11px] text-slate-400">防重 Token (Idempotency Key):</label>
                    <button
                      onClick={() => setIdempotencyKey(`IDEMP-${Date.now().toString().slice(-6)}`)}
                      className="text-[10px] text-blue-400 hover:underline"
                    >
                      生成新Key
                    </button>
                  </div>
                  <input
                    type="text"
                    value={idempotencyKey}
                    onChange={(e) => setIdempotencyKey(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200 font-mono"
                  />
                </div>

                <div>
                  <label className="text-[11px] text-slate-400 block mb-1">扣款/交易金额 (CNY):</label>
                  <input
                    type="number"
                    value={orderAmount}
                    onChange={(e) => setOrderAmount(Number(e.target.value))}
                    className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200 font-mono"
                  />
                </div>
              </div>
            </div>

            <div className="mt-4">
              <button
                onClick={handleTestIdempotency}
                disabled={sandbox2Loading}
                className="w-full py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-lg text-xs font-semibold shadow-sm transition"
              >
                发起业务请求 (多次点击测试防重)
              </button>

              {sandbox2Result && (
                <div
                  className={`mt-2.5 p-2.5 rounded border text-[11px] font-mono ${
                    sandbox2Result.hitCache
                      ? 'bg-amber-500/10 border-amber-500/30 text-amber-300'
                      : 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                  }`}
                >
                  <div className="font-semibold mb-1">
                    {sandbox2Result.message}
                  </div>
                  <pre className="text-[10px] overflow-x-auto text-slate-300 mt-1">
                    {JSON.stringify(
                      sandbox2Result.hitCache ? sandbox2Result.originalResult : sandbox2Result.executionResult,
                      null,
                      2
                    )}
                  </pre>
                </div>
              )}
            </div>
          </div>

          {/* SANDBOX 3: Zone-Aware Routing */}
          <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col justify-between">
            <div>
              <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                <span className="font-semibold text-xs text-emerald-300 flex items-center gap-1.5">
                  <Compass className="w-4 h-4" />
                  沙盒 3: 多可用区就近路由
                </span>
                <span className="text-[10px] bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded">Zone-Affinity</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-2">
                同机房调用优先，节省 15~25ms 跨机房往返网络开销；仅在本地节点故障时降级跨机房。
              </p>

              <div className="mt-3 space-y-2 text-xs">
                <div>
                  <label className="text-[11px] text-slate-400 block mb-1">当前发起方所在机房/可用区:</label>
                  <select
                    value={callerZone}
                    onChange={(e: any) => setCallerZone(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200 font-mono"
                  >
                    <option value="zone-east-1a">zone-east-1a (华东1区机房 A)</option>
                    <option value="zone-east-1b">zone-east-1b (华东1区机房 B)</option>
                  </select>
                </div>

                <div className="text-[11px] text-slate-500">
                  当前集群服务池中节点包含 1a (Node-01) 与 1b (Node-02)。
                </div>
              </div>
            </div>

            <div className="mt-4">
              <button
                onClick={handleTestZoneRouting}
                disabled={sandbox3Loading}
                className="w-full py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold shadow-sm transition"
              >
                执行就近亲和性路由解析
              </button>

              {sandbox3Result && (
                <div className="mt-2.5 p-2.5 bg-slate-900 rounded border border-emerald-500/30 text-[11px] font-mono text-slate-300">
                  <div
                    className={`font-semibold mb-1 ${
                      sandbox3Result.isAffinityHit ? 'text-emerald-400' : 'text-amber-400'
                    }`}
                  >
                    {sandbox3Result.message}
                  </div>
                  <div className="text-[10px] text-slate-400 mt-1">
                    命中实例: <strong className="text-slate-200">{sandbox3Result.selectedEndpoint.id}</strong> (
                    {sandbox3Result.selectedEndpoint.address} - Zone: {sandbox3Result.selectedEndpoint.zone})
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* SANDBOX 4: Dual-Track Event Contract Isolation Guardrail */}
          <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col justify-between">
            <div>
              <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                <span className="font-semibold text-xs text-rose-300 flex items-center gap-1.5">
                  <ShieldAlert className="w-4 h-4" />
                  沙盒 4: 事件契约安全守卫
                </span>
                <span className="text-[10px] bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded">Event Contract</span>
              </div>
              <p className="text-[11px] text-slate-400 mt-2">
                隔离进程内事件与分布式发件箱事件，静态类型与运行时双重拦截非法穿透。
              </p>

              <div className="mt-3 space-y-2 text-xs">
                <div>
                  <label className="text-[11px] text-slate-400 block mb-1">选择待发布的事件契约类型:</label>
                  <select
                    value={sandbox4Contract}
                    onChange={(e: any) => setSandbox4Contract(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200 font-mono"
                  >
                    <option value="ILocalDomainEvent">ILocalDomainEvent (进程内本地领域事件)</option>
                    <option value="IIntegrationEvent">IIntegrationEvent (跨服务分布式事务事件)</option>
                  </select>
                </div>

                <div>
                  <label className="text-[11px] text-slate-400 block mb-1">发布目标 Topic:</label>
                  <input
                    type="text"
                    value={sandbox4Topic}
                    onChange={(e) => setSandbox4Topic(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-xs text-slate-200 font-mono"
                  />
                </div>
              </div>
            </div>

            <div className="mt-4">
              <button
                onClick={handleTestEventGuardrail}
                disabled={sandbox4Loading}
                className="w-full py-2 bg-rose-600/80 hover:bg-rose-600 text-white rounded-lg text-xs font-semibold shadow-sm transition"
              >
                测试事件发布并触发守卫检测
              </button>

              {sandbox4Result && (
                <div
                  className={`mt-2.5 p-2.5 rounded border text-[11px] font-mono ${
                    sandbox4Result.violation
                      ? 'bg-rose-500/10 border-rose-500/30 text-rose-300'
                      : 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                  }`}
                >
                  <div className="font-semibold mb-1">
                    {sandbox4Result.message}
                  </div>
                  <p className="text-[10px] text-slate-300 mt-0.5 leading-snug">
                    {sandbox4Result.details}
                  </p>
                  {sandbox4Result.remedy && (
                    <div className="mt-1.5 text-[9.5px] text-amber-300 bg-amber-500/10 p-1.5 rounded border border-amber-500/20">
                      💡 治理方案: {sandbox4Result.remedy}
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* SECTION: .NET 10 (net10.0 LTS) 全景架构突破与性能飞跃 */}
      {dotNet10Spec && (
        <div className="bg-slate-900 border border-indigo-500/30 rounded-xl p-6 shadow-sm space-y-5">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
            <div>
              <div className="flex items-center gap-2">
                <span className="p-1.5 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                  <Zap className="w-4 h-4" />
                </span>
                <h3 className="text-sm font-semibold text-slate-100 flex items-center gap-2">
                  .NET 10 (net10.0 LTS) 全景架构突破与性能飞跃
                  <span className="text-[10px] font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 px-2 py-0.5 rounded">
                    C# 14 • Native AOT Ready
                  </span>
                </h3>
              </div>
              <p className="text-xs text-slate-400 mt-1">
                Aspros.Core 框架已全量升级为 .NET 10 LTS 基线，引入 C# 14 原生 Lock、HybridCache 混合双层缓存、Frozen 只读哈希表与 Vector512 SIMD 向量化计算。
              </p>
            </div>

            <div className="flex items-center gap-2 text-xs">
              <span className="px-2.5 py-1 rounded bg-slate-950 text-indigo-300 border border-indigo-500/30 font-mono text-[11px]">
                Target: {dotNet10Spec.targetFramework}
              </span>
              <span className="px-2.5 py-1 rounded bg-slate-950 text-emerald-300 border border-emerald-500/30 font-mono text-[11px]">
                {dotNet10Spec.runtimeVersion}
              </span>
            </div>
          </div>

          {/* 5 Big Feature Breakdowns */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3.5">
            {dotNet10Spec.features.map((feat, idx) => (
              <div
                key={idx}
                className="bg-slate-950 p-4 rounded-xl border border-slate-800/90 hover:border-indigo-500/50 transition flex flex-col justify-between"
              >
                <div>
                  <div className="flex items-center justify-between pb-2 border-b border-slate-800 text-xs">
                    <span className="font-semibold text-slate-200 flex items-center gap-1.5">
                      <Sparkles className="w-3.5 h-3.5 text-indigo-400" />
                      {feat.name}
                    </span>
                    <span className="text-[10px] font-mono bg-slate-800 text-slate-300 px-1.5 py-0.5 rounded">
                      {feat.category}
                    </span>
                  </div>

                  <div className="mt-2.5 space-y-2 text-xs">
                    <div>
                      <div className="text-[10px] text-slate-500 uppercase tracking-wider">.NET 10 核心提升:</div>
                      <div className="text-slate-300 text-[11px] leading-relaxed mt-0.5">{feat.inDotNet10}</div>
                    </div>

                    <div className="bg-slate-900/90 p-2.5 rounded-lg border border-slate-800">
                      <div className="text-[10px] text-emerald-400 font-semibold flex items-center gap-1">
                        <Activity className="w-3 h-3" /> 基准实测收益:
                      </div>
                      <div className="text-[11px] text-emerald-300 font-mono mt-0.5">{feat.benchmarkImpact}</div>
                    </div>
                  </div>
                </div>

                <div className="mt-3 pt-2.5 border-t border-slate-800/70">
                  <pre className="p-2 bg-slate-900 rounded font-mono text-[10px] text-indigo-300 overflow-x-auto whitespace-pre-wrap">
                    {feat.codeSnippet}
                  </pre>
                </div>
              </div>
            ))}
          </div>

          {/* Assembly Target Compilation Table */}
          <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 space-y-3">
            <div className="flex items-center justify-between text-xs pb-2 border-b border-slate-800">
              <span className="font-semibold text-slate-200 flex items-center gap-1.5">
                <Server className="w-4 h-4 text-emerald-400" />
                Aspros Framework 组件程序集编译目标矩阵 (.NET 10 Target Matrix)
              </span>
              <span className="text-[10px] text-emerald-400 font-mono bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
                100% TargetFramework: net10.0
              </span>
            </div>

            <div className="overflow-x-auto border border-slate-800/80 rounded-lg">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-900/90 text-slate-400 font-mono text-[11px] border-b border-slate-800">
                  <tr>
                    <th className="p-2.5 font-semibold">模块工程 (Project)</th>
                    <th className="p-2.5 font-semibold">产物程序集 (Assembly)</th>
                    <th className="p-2.5 font-semibold">.NET 10 现代特性适配</th>
                    <th className="p-2.5 font-semibold text-right">运行状态</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 bg-slate-950 font-mono text-[11px]">
                  {dotNet10Spec.upgradedModules.map((mod, idx) => (
                    <tr key={idx} className="hover:bg-slate-900/40 transition">
                      <td className="p-2.5 text-slate-200 font-semibold">{mod.moduleName}</td>
                      <td className="p-2.5 text-slate-400">{mod.assembly}</td>
                      <td className="p-2.5 text-slate-300 font-sans text-xs">{mod.net10Optimization}</td>
                      <td className="p-2.5 text-right">
                        <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
                          {mod.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
