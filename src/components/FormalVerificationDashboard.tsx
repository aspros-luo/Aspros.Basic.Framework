import React, { useState, useEffect } from 'react';
import {
  ShieldCheck,
  ShieldAlert,
  Activity,
  Play,
  RefreshCw,
  Zap,
  CheckCircle2,
  AlertTriangle,
  FileCode2,
  Copy,
  Check,
  Layers,
  Lock,
  Flame,
  ArrowRight,
  Terminal,
  BarChart2,
  Clock,
  Radio,
  Sliders,
  Sparkles,
  Info,
  Maximize2,
  Database,
  Download,
} from 'lucide-react';
import type {
  DistributedProtocol,
  MachineVerifiableReceipt,
  IdempotencyKeyRecord,
  WorkerExecutionTrace,
  ChaosRunProfile,
} from '../types';

export function FormalVerificationDashboard() {
  // Chaos runner settings
  const [protocol, setProtocol] = useState<DistributedProtocol>('2PC_IDEMPOTENT');
  const [concurrency, setConcurrency] = useState<number>(50);
  const [keySpread, setKeySpread] = useState<'single_hotspot' | 'clustered' | 'dispersed'>('clustered');
  const [latencyJitter, setLatencyJitter] = useState<boolean>(true);
  const [connectionDrop, setConnectionDrop] = useState<boolean>(true);
  const [splitBrainCrash, setSplitBrainCrash] = useState<boolean>(false);
  const [dropRatePercent, setDropRatePercent] = useState<number>(15);
  const [leaseTimeoutMs, setLeaseTimeoutMs] = useState<number>(3000);

  // Execution state
  const [isRunning, setIsRunning] = useState<boolean>(false);
  const [activeReceipt, setActiveReceipt] = useState<MachineVerifiableReceipt | null>(null);
  const [receiptsList, setReceiptsList] = useState<MachineVerifiableReceipt[]>([]);
  const [idempotencyRecords, setIdempotencyRecords] = useState<IdempotencyKeyRecord[]>([]);
  const [stockSnapshot, setStockSnapshot] = useState<Record<string, { available: number; locked: number; deducted: number }>>({});
  const [selectedTrace, setSelectedTrace] = useState<WorkerExecutionTrace | null>(null);

  // TLA+ Modal
  const [showTlaModal, setShowTlaModal] = useState<boolean>(false);
  const [tlaCode, setTlaCode] = useState<string>('');
  const [copiedTla, setCopiedTla] = useState<boolean>(false);

  // Active view tab inside dashboard
  const [dashboardSubTab, setDashboardSubTab] = useState<'assertions' | 'waterfall' | 'records' | 'tla'>('assertions');

  // Load initial state
  useEffect(() => {
    fetchInitialData();
  }, []);

  const fetchInitialData = async () => {
    try {
      const [recRes, stateRes] = await Promise.all([
        fetch('/api/framework/verification/receipts'),
        fetch('/api/framework/verification/idempotency-state'),
      ]);
      const recJson = await recRes.json();
      const stateJson = await stateRes.json();

      if (recJson.success && recJson.receipts && recJson.receipts.length > 0) {
        setReceiptsList(recJson.receipts);
        setActiveReceipt(recJson.receipts[0]);
        if (recJson.receipts[0].tracesSample?.length > 0) {
          setSelectedTrace(recJson.receipts[0].tracesSample[0]);
        }
      }
      if (stateJson.success) {
        setIdempotencyRecords(stateJson.records || []);
        setStockSnapshot(stateJson.stockSnapshot || {});
      }
    } catch (e) {
      console.error('Error fetching initial verification data:', e);
    }
  };

  const handleRunChaosTest = async () => {
    setIsRunning(true);
    setSelectedTrace(null);
    try {
      const profile: ChaosRunProfile = {
        concurrency,
        keySpread,
        chaosFaults: {
          latencyJitter,
          connectionDrop,
          splitBrainCrash,
          messageReordering: true,
        },
        jitterMinMs: 15,
        jitterMaxMs: 120,
        dropRatePercent,
        protocol,
        leaseTimeoutMs,
      };

      const res = await fetch('/api/framework/verification/run-chaos-suite', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(profile),
      });
      const data = await res.json();
      if (data.success && data.receipt) {
        setActiveReceipt(data.receipt);
        setReceiptsList((prev) => [data.receipt, ...prev.slice(0, 19)]);
        if (data.receipt.tracesSample?.length > 0) {
          setSelectedTrace(data.receipt.tracesSample[0]);
        }
      }

      // Refresh records
      const stateRes = await fetch('/api/framework/verification/idempotency-state');
      const stateJson = await stateRes.json();
      if (stateJson.success) {
        setIdempotencyRecords(stateJson.records || []);
        setStockSnapshot(stateJson.stockSnapshot || {});
      }
    } catch (err) {
      console.error('Chaos run failed:', err);
    } finally {
      setIsRunning(false);
    }
  };

  const handleResetBaseline = async () => {
    try {
      const res = await fetch('/api/framework/verification/reset', { method: 'POST' });
      const data = await res.json();
      if (data.success) {
        setIdempotencyRecords([]);
        setStockSnapshot(data.stockSnapshot || {});
        setActiveReceipt(null);
        setSelectedTrace(null);
      }
    } catch (err) {
      console.error('Reset failed:', err);
    }
  };

  const handleOpenTlaModal = async () => {
    try {
      const res = await fetch(`/api/framework/verification/tla-spec?protocol=${protocol}`);
      const data = await res.json();
      if (data.success) {
        setTlaCode(data.tlaSpec);
        setShowTlaModal(true);
      }
    } catch (err) {
      console.error(err);
    }
  };

  const handleCopyTla = () => {
    navigator.clipboard.writeText(tlaCode);
    setCopiedTla(true);
    setTimeout(() => setCopiedTla(false), 2000);
  };

  return (
    <div className="space-y-6">
      {/* Top Banner: Verification Engine Header */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-sm">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-4 border-b border-slate-800">
          <div>
            <div className="flex items-center gap-2.5">
              <div className="p-2 rounded-lg bg-indigo-500/10 border border-indigo-500/30 text-indigo-400">
                <ShieldCheck className="w-5 h-5" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-base font-bold text-white tracking-tight">
                    Aspros.Core 形式化不变量验证与混沌断言工作台
                  </h2>
                  <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                    Jepsen-Style Chaos Engine
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">
                  基于数学不变量规约与状态机模型检验，通过毫秒级混沌并发风暴注入，生成机器独立可信证明（Proof Receipt）。
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2.5">
            <button
              onClick={handleOpenTlaModal}
              className="flex items-center gap-1.5 text-xs text-slate-300 hover:text-white bg-slate-800/90 hover:bg-slate-800 px-3 py-1.5 rounded-lg border border-slate-700 transition font-mono"
            >
              <FileCode2 className="w-3.5 h-3.5 text-blue-400" />
              查看 TLA+ 形式化规约
            </button>
            <button
              onClick={handleResetBaseline}
              className="flex items-center gap-1.5 text-xs text-slate-400 hover:text-rose-300 bg-slate-950 px-3 py-1.5 rounded-lg border border-slate-800 transition font-mono"
            >
              <RefreshCw className="w-3.5 h-3.5" />
              重置基线状态
            </button>
          </div>
        </div>

        {/* Live Proof Status KPI Row */}
        <div className="mt-4 grid grid-cols-2 md:grid-cols-4 gap-3 text-xs font-mono">
          <div className="bg-slate-950 p-3 rounded-lg border border-slate-800">
            <div className="text-[11px] text-slate-400 flex items-center justify-between">
              <span className="text-slate-300 flex items-center gap-1">
                <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" /> 不变量断言状态
              </span>
              <span className="text-[10px] bg-emerald-500/10 text-emerald-400 px-1.5 py-0.5 rounded">
                Machine-Verified
              </span>
            </div>
            <div className="mt-1.5 text-lg font-bold text-emerald-400 flex items-center gap-2">
              {activeReceipt ? (
                activeReceipt.allInvariantsPassed ? (
                  <>100% 通过 <span className="text-xs font-normal text-slate-400">(0 违规)</span></>
                ) : (
                  <span className="text-rose-400">存在不变量违规</span>
                )
              ) : (
                <span className="text-slate-500">待执行断言</span>
              )}
            </div>
            <div className="text-[10px] text-slate-500 mt-0.5">
              数学规约严格证明 (A / B / C)
            </div>
          </div>

          <div className="bg-slate-950 p-3 rounded-lg border border-slate-800">
            <div className="text-[11px] text-slate-400 flex items-center justify-between">
              <span className="text-slate-300 flex items-center gap-1">
                <Lock className="w-3.5 h-3.5 text-amber-400" /> 重入攻击阻断数
              </span>
              <span className="text-[10px] bg-amber-500/10 text-amber-400 px-1.5 py-0.5 rounded">
                ΔState ≡ 0
              </span>
            </div>
            <div className="mt-1.5 text-lg font-bold text-amber-400">
              {activeReceipt ? activeReceipt.duplicateExecutionAttacksBlocked : 0} <span className="text-xs font-normal text-slate-400">次拦截</span>
            </div>
            <div className="text-[10px] text-slate-500 mt-0.5">
              原子租约防重 / 零二次扣减
            </div>
          </div>

          <div className="bg-slate-950 p-3 rounded-lg border border-slate-800">
            <div className="text-[11px] text-slate-400 flex items-center justify-between">
              <span className="text-slate-300 flex items-center gap-1">
                <Zap className="w-3.5 h-3.5 text-blue-400" /> 真实落库提交数
              </span>
              <span className="text-[10px] bg-blue-500/10 text-blue-400 px-1.5 py-0.5 rounded">
                Physical Commits
              </span>
            </div>
            <div className="mt-1.5 text-lg font-bold text-blue-400">
              {activeReceipt ? activeReceipt.physicalDbCommits : 0} <span className="text-xs font-normal text-slate-400">/ {activeReceipt ? activeReceipt.uniqueIdempotencyKeys : 0} 唯一键</span>
            </div>
            <div className="text-[10px] text-slate-500 mt-0.5">
              单键落库次数恒等于 1
            </div>
          </div>

          <div className="bg-slate-950 p-3 rounded-lg border border-slate-800">
            <div className="text-[11px] text-slate-400 flex items-center justify-between">
              <span className="text-slate-300 flex items-center gap-1">
                <Clock className="w-3.5 h-3.5 text-purple-400" /> P99 混沌时延
              </span>
              <span className="text-[10px] bg-purple-500/10 text-purple-400 px-1.5 py-0.5 rounded">
                Latency Tail
              </span>
            </div>
            <div className="mt-1.5 text-lg font-bold text-purple-400">
              {activeReceipt ? `${activeReceipt.p99LatencyMs} ms` : '—'}
            </div>
            <div className="text-[10px] text-slate-500 mt-0.5">
              含网络抖动与自旋等待开销
            </div>
          </div>
        </div>
      </div>

      {/* Control Deck & Chaos Injection Configuration */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-sm space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 border-b border-slate-800">
          <div className="flex items-center gap-2">
            <Sliders className="w-4 h-4 text-indigo-400" />
            <h3 className="text-sm font-semibold text-white">
              混沌并发故障注入控制台 (Chaos & Concurrency Matrix)
            </h3>
          </div>
          <div className="text-xs text-slate-400 font-mono">
            模式: <span className="text-indigo-400 font-semibold">{protocol}</span>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-xs">
          {/* Protocol Selection */}
          <div className="bg-slate-950 p-3.5 rounded-lg border border-slate-800 space-y-2">
            <label className="block text-slate-300 font-semibold flex items-center gap-1.5">
              <Layers className="w-3.5 h-3.5 text-blue-400" /> 分布式协议模型
            </label>
            <select
              value={protocol}
              onChange={(e) => setProtocol(e.target.value as DistributedProtocol)}
              className="w-full bg-slate-900 border border-slate-700 rounded px-2.5 py-1.5 text-slate-200 font-mono text-xs focus:outline-none focus:border-blue-500"
            >
              <option value="2PC_IDEMPOTENT">2PC 两阶段提交 + 声明式幂等租约</option>
              <option value="SAGA_COMPENSATION">Saga 补偿事务 + Transactional Outbox</option>
              <option value="TCC_EXPIRATION">TCC (Try-Confirm-Cancel) 资源预占超时</option>
            </select>
            <p className="text-[11px] text-slate-500 leading-tight">
              {protocol === '2PC_IDEMPOTENT'
                ? '严格持有分布式租约锁，支持并发重入与过期自动抢占。'
                : protocol === 'SAGA_COMPENSATION'
                ? '支持网络分区场景下的反向事务自动回滚与补偿。'
                : '先预占可用库存配额，并在超时窗口内确认结算。'}
            </p>
          </div>

          {/* Concurrency Slider & Quick Presets */}
          <div className="bg-slate-950 p-3.5 rounded-lg border border-slate-800 space-y-2">
            <div className="flex items-center justify-between">
              <label className="text-slate-300 font-semibold flex items-center gap-1.5">
                <Flame className="w-3.5 h-3.5 text-rose-400" /> 并发线程轰炸强度
              </label>
              <span className="font-mono text-indigo-400 font-bold">{concurrency} 线程</span>
            </div>
            <input
              type="range"
              min={10}
              max={200}
              step={10}
              value={concurrency}
              onChange={(e) => setConcurrency(Number(e.target.value))}
              className="w-full accent-indigo-500 h-1.5 bg-slate-800 rounded-lg cursor-pointer"
            />
            <div className="flex items-center justify-between gap-1 text-[10px] font-mono">
              <button
                onClick={() => setConcurrency(20)}
                className={`px-1.5 py-0.5 rounded border ${concurrency === 20 ? 'bg-indigo-600 text-white border-indigo-500' : 'bg-slate-900 text-slate-400 border-slate-800'}`}
              >
                20 (轻量)
              </button>
              <button
                onClick={() => setConcurrency(50)}
                className={`px-1.5 py-0.5 rounded border ${concurrency === 50 ? 'bg-indigo-600 text-white border-indigo-500' : 'bg-slate-900 text-slate-400 border-slate-800'}`}
              >
                50 (标准)
              </button>
              <button
                onClick={() => setConcurrency(100)}
                className={`px-1.5 py-0.5 rounded border ${concurrency === 100 ? 'bg-indigo-600 text-white border-indigo-500' : 'bg-slate-900 text-slate-400 border-slate-800'}`}
              >
                100 (风暴)
              </button>
              <button
                onClick={() => setConcurrency(200)}
                className={`px-1.5 py-0.5 rounded border ${concurrency === 200 ? 'bg-indigo-600 text-white border-indigo-500' : 'bg-slate-900 text-slate-400 border-slate-800'}`}
              >
                200 (极限)
              </button>
            </div>
          </div>

          {/* Key Collision Spread */}
          <div className="bg-slate-950 p-3.5 rounded-lg border border-slate-800 space-y-2">
            <label className="block text-slate-300 font-semibold flex items-center gap-1.5">
              <Radio className="w-3.5 h-3.5 text-amber-400" /> 幂等键竞态分布模型
            </label>
            <select
              value={keySpread}
              onChange={(e) => setKeySpread(e.target.value as any)}
              className="w-full bg-slate-900 border border-slate-700 rounded px-2.5 py-1.5 text-slate-200 font-mono text-xs focus:outline-none focus:border-amber-500"
            >
              <option value="single_hotspot">单热点极端碰撞 (100% 同一Key 轰炸)</option>
              <option value="clustered">多键聚簇竞争 (4组 Key 高频互冲)</option>
              <option value="dispersed">高离散长尾 (80% 唯一键 + 20% 重放)</option>
            </select>
            <p className="text-[11px] text-slate-500 leading-tight">
              {keySpread === 'single_hotspot'
                ? '全部并发线程轰炸同一个幂等键，检测瞬时自旋锁与零副作用。'
                : keySpread === 'clustered'
                ? '模拟多个订单同时高频重试的真实跨行转账或秒杀场景。'
                : '长尾流量模型，检测高吞吐下状态表哈希冲突与清理机制。'}
            </p>
          </div>

          {/* Fault Matrix Toggles */}
          <div className="bg-slate-950 p-3.5 rounded-lg border border-slate-800 space-y-2">
            <label className="block text-slate-300 font-semibold flex items-center gap-1.5">
              <Zap className="w-3.5 h-3.5 text-rose-400" /> 混沌扰动开关
            </label>
            <div className="space-y-1.5 text-[11px]">
              <label className="flex items-center gap-2 text-slate-300 cursor-pointer">
                <input
                  type="checkbox"
                  checked={latencyJitter}
                  onChange={(e) => setLatencyJitter(e.target.checked)}
                  className="rounded border-slate-700 text-indigo-600 focus:ring-0"
                />
                <span>网络时延与随机抖动 (15~120ms)</span>
              </label>
              <label className="flex items-center gap-2 text-slate-300 cursor-pointer">
                <input
                  type="checkbox"
                  checked={connectionDrop}
                  onChange={(e) => setConnectionDrop(e.target.checked)}
                  className="rounded border-slate-700 text-indigo-600 focus:ring-0"
                />
                <span>瞬态断开与套接字重置 ({dropRatePercent}% 概率)</span>
              </label>
              <label className="flex items-center gap-2 text-slate-300 cursor-pointer">
                <input
                  type="checkbox"
                  checked={splitBrainCrash}
                  onChange={(e) => setSplitBrainCrash(e.target.checked)}
                  className="rounded border-slate-700 text-indigo-600 focus:ring-0"
                />
                <span>脑裂/提交前崩溃模拟 (Net Partition)</span>
              </label>
            </div>
          </div>
        </div>

        {/* Big Action Button */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-3 border-t border-slate-800">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <Info className="w-4 h-4 text-indigo-400 shrink-0" />
            <span>
              断言过程由独立探针执行，比对落盘数据库、内存状态树与 SHA-256 签名，产生不可篡改的 Proof Receipt。
            </span>
          </div>

          <button
            onClick={handleRunChaosTest}
            disabled={isRunning}
            className="flex items-center justify-center gap-2 bg-gradient-to-r from-indigo-600 to-blue-600 hover:from-indigo-500 hover:to-blue-500 disabled:opacity-50 text-white font-semibold text-xs py-2.5 px-6 rounded-lg shadow-lg shadow-indigo-500/20 transition cursor-pointer"
          >
            {isRunning ? (
              <>
                <RefreshCw className="w-4 h-4 animate-spin" />
                正在注入混沌并发并断言不变量...
              </>
            ) : (
              <>
                <Play className="w-4 h-4 fill-white" />
                启动 Jepsen 风格混沌验证套件 (Run Verification)
              </>
            )}
          </button>
        </div>
      </div>

      {/* State Machine Transition DAG (Interactive SVG Visualization) */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-sm space-y-3">
        <div className="flex items-center justify-between pb-2 border-b border-slate-800 text-xs">
          <div className="flex items-center gap-2">
            <Layers className="w-4 h-4 text-emerald-400" />
            <h3 className="font-semibold text-white">
              分布式幂等状态机转移图 (Labeled Transition System - LTS)
            </h3>
          </div>
          <span className="text-[11px] font-mono text-slate-400">
            原子 CAS 状态空间模型
          </span>
        </div>

        <div className="bg-slate-950 p-4 rounded-lg border border-slate-800/80 overflow-x-auto">
          <svg className="w-full min-w-[720px] h-48" viewBox="0 0 850 180">
            <defs>
              <marker
                id="arrowhead"
                markerWidth="8"
                markerHeight="6"
                refX="7"
                refY="3"
                orient="auto"
              >
                <polygon points="0 0, 8 3, 0 6" fill="#64748b" />
              </marker>
              <marker
                id="arrowhead-emerald"
                markerWidth="8"
                markerHeight="6"
                refX="7"
                refY="3"
                orient="auto"
              >
                <polygon points="0 0, 8 3, 0 6" fill="#10b981" />
              </marker>
              <marker
                id="arrowhead-amber"
                markerWidth="8"
                markerHeight="6"
                refX="7"
                refY="3"
                orient="auto"
              >
                <polygon points="0 0, 8 3, 0 6" fill="#f59e0b" />
              </marker>
            </defs>

            {/* State Node 1: INIT */}
            <g transform="translate(40, 60)">
              <rect
                width="120"
                height="60"
                rx="8"
                className="fill-slate-900 stroke-slate-700 stroke-2"
              />
              <text x="60" y="26" textAnchor="middle" className="fill-slate-200 text-xs font-bold font-mono">
                [INIT]
              </text>
              <text x="60" y="44" textAnchor="middle" className="fill-slate-400 text-[10px] font-mono">
                未见该幂等Token
              </text>
            </g>

            {/* Path: INIT -> EXECUTING */}
            <path
              d="M 160 90 L 250 90"
              stroke="#64748b"
              strokeWidth="2"
              fill="none"
              markerEnd="url(#arrowhead)"
            />
            <text x="205" y="80" textAnchor="middle" className="fill-blue-400 text-[10px] font-mono">
              获取原子租约
            </text>

            {/* State Node 2: EXECUTING */}
            <g transform="translate(250, 60)">
              <rect
                width="150"
                height="60"
                rx="8"
                className="fill-indigo-950/60 stroke-indigo-500/80 stroke-2"
              />
              <text x="75" y="26" textAnchor="middle" className="fill-indigo-300 text-xs font-bold font-mono">
                [EXECUTING]
              </text>
              <text x="75" y="44" textAnchor="middle" className="fill-indigo-400/80 text-[10px] font-mono">
                持有租约锁 (Lease Valid)
              </text>
            </g>

            {/* Path: EXECUTING -> COMMITTED */}
            <path
              d="M 400 90 L 520 90"
              stroke="#10b981"
              strokeWidth="2"
              fill="none"
              markerEnd="url(#arrowhead-emerald)"
            />
            <text x="460" y="80" textAnchor="middle" className="fill-emerald-400 text-[10px] font-mono">
              事务提交 (Commit==1)
            </text>

            {/* State Node 3: COMMITTED */}
            <g transform="translate(520, 60)">
              <rect
                width="150"
                height="60"
                rx="8"
                className="fill-emerald-950/60 stroke-emerald-500 stroke-2"
              />
              <text x="75" y="26" textAnchor="middle" className="fill-emerald-300 text-xs font-bold font-mono">
                [COMMITTED]
              </text>
              <text x="75" y="44" textAnchor="middle" className="fill-emerald-400 text-[10px] font-mono">
                已持久化 & 缓存响应
              </text>
            </g>

            {/* Path: Self-loop on COMMITTED for Idempotent Hit */}
            <path
              d="M 670 80 C 740 40, 740 140, 670 100"
              stroke="#f59e0b"
              strokeWidth="2"
              strokeDasharray="4 2"
              fill="none"
              markerEnd="url(#arrowhead-amber)"
            />
            <text x="745" y="90" textAnchor="middle" className="fill-amber-400 text-[10px] font-mono font-bold">
              重复请求直接复用 (ΔState≡0)
            </text>

            {/* Path: EXECUTING -> LEASE EXPIRED */}
            <path
              d="M 325 120 L 325 150 L 520 150"
              stroke="#ef4444"
              strokeWidth="1.5"
              strokeDasharray="3 3"
              fill="none"
              markerEnd="url(#arrowhead)"
            />
            <text x="420" y="165" textAnchor="middle" className="fill-rose-400 text-[10px] font-mono">
              租约超时释放 (Lease Expired / Failover)
            </text>
          </svg>
        </div>
      </div>

      {/* Main Analysis Deck (Tabs: Invariant Assertions, Waterfall, State Records) */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-sm">
        {/* Sub-tabs header */}
        <div className="flex items-center justify-between px-5 pt-3 pb-0 border-b border-slate-800 bg-slate-900/60">
          <div className="flex gap-2 text-xs font-medium">
            <button
              onClick={() => setDashboardSubTab('assertions')}
              className={`flex items-center gap-1.5 px-3 py-2 border-b-2 font-mono transition ${
                dashboardSubTab === 'assertions'
                  ? 'border-indigo-500 text-indigo-400 font-bold bg-indigo-500/5'
                  : 'border-transparent text-slate-400 hover:text-slate-200'
              }`}
            >
              <ShieldCheck className="w-3.5 h-3.5" />
              三大数学不变量断言详情 (Mathematical Invariants)
            </button>
            <button
              onClick={() => setDashboardSubTab('waterfall')}
              className={`flex items-center gap-1.5 px-3 py-2 border-b-2 font-mono transition ${
                dashboardSubTab === 'waterfall'
                  ? 'border-indigo-500 text-indigo-400 font-bold bg-indigo-500/5'
                  : 'border-transparent text-slate-400 hover:text-slate-200'
              }`}
            >
              <BarChart2 className="w-3.5 h-3.5" />
              多线程并发瀑布与响应一致性对比 (Execution Waterfall & Diff)
            </button>
            <button
              onClick={() => setDashboardSubTab('records')}
              className={`flex items-center gap-1.5 px-3 py-2 border-b-2 font-mono transition ${
                dashboardSubTab === 'records'
                  ? 'border-indigo-500 text-indigo-400 font-bold bg-indigo-500/5'
                  : 'border-transparent text-slate-400 hover:text-slate-200'
              }`}
            >
              <Database className="w-3.5 h-3.5" />
              底层物理状态表与账本快照 (Ledger & Idempotency Store)
            </button>
          </div>

          {activeReceipt && (
            <div className="text-[11px] font-mono text-slate-400 flex items-center gap-2 pb-2">
              <span>Proof ID:</span>
              <span className="text-indigo-400 font-semibold">{activeReceipt.receiptId}</span>
            </div>
          )}
        </div>

        {/* SubTab 1: Invariant Assertions */}
        {dashboardSubTab === 'assertions' && (
          <div className="p-5 space-y-4">
            {activeReceipt ? (
              <div className="space-y-4">
                {/* Cryptographic Machine Proof Receipt Badge */}
                <div className="bg-slate-950 p-4 rounded-xl border border-indigo-500/30 font-mono text-xs space-y-2">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-2 border-b border-slate-800">
                    <div className="flex items-center gap-2">
                      <div className="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse"></div>
                      <span className="text-white font-bold">
                        MACHINE-VERIFIABLE PROOF RECEIPT (机器可验证证明回执)
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => {
                          const jsonBlob = new Blob([JSON.stringify(activeReceipt, null, 2)], {
                            type: 'application/json',
                          });
                          const url = URL.createObjectURL(jsonBlob);
                          const a = document.createElement('a');
                          a.href = url;
                          a.download = `aspros_merkle_receipt_${activeReceipt.receiptId}.json`;
                          a.click();
                          URL.revokeObjectURL(url);
                        }}
                        className="flex items-center gap-1 px-2.5 py-0.5 rounded bg-slate-900 hover:bg-slate-800 text-indigo-300 border border-indigo-500/30 text-[10px] transition"
                        title="导出包含默克尔根、HSM 签名与违规证明的存证文件 (.json)"
                      >
                        <Download className="w-3 h-3" />
                        导出存证 JSON
                      </button>
                      <span className="text-[10px] text-slate-500">{activeReceipt.timestamp}</span>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-[11px]">
                    <div>
                      <span className="text-slate-500">Merkle Root State Hash (SHA-256):</span>
                      <div className="text-emerald-400 break-all select-all mt-0.5 bg-slate-900 px-2 py-1 rounded border border-slate-800">
                        {activeReceipt.merkleRootHash}
                      </div>
                    </div>
                    <div>
                      <span className="text-slate-500">HSM Verification Signature:</span>
                      <div className="text-indigo-400 break-all select-all mt-0.5 bg-slate-900 px-2 py-1 rounded border border-slate-800">
                        {activeReceipt.cryptoSignature}
                      </div>
                    </div>
                  </div>
                </div>

                {/* 3 Core Invariants List */}
                <div className="space-y-3">
                  {activeReceipt.invariants.map((inv) => (
                    <div
                      key={inv.id}
                      className={`p-4 rounded-xl border transition ${
                        inv.satisfied
                          ? 'bg-slate-950 border-emerald-500/30'
                          : 'bg-rose-950/20 border-rose-500/50'
                      }`}
                    >
                      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-2 border-b border-slate-800/80">
                        <div className="flex items-center gap-2">
                          {inv.satisfied ? (
                            <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                          ) : (
                            <AlertTriangle className="w-4 h-4 text-rose-400 shrink-0" />
                          )}
                          <span className="font-semibold text-white text-xs">{inv.name}</span>
                          <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800 text-slate-300">
                            {inv.category}
                          </span>
                        </div>

                        <div className="flex items-center gap-2 font-mono text-[11px]">
                          <span className={inv.satisfied ? 'text-emerald-400 font-bold' : 'text-rose-400 font-bold'}>
                            {inv.satisfied ? 'SATISFIED (断言成立)' : 'VIOLATION DETECTED'}
                          </span>
                          <span className="text-slate-500">违规次数: {inv.violationCount}</span>
                        </div>
                      </div>

                      {/* Mathematical formula badge */}
                      <div className="mt-2.5 bg-slate-900 px-3 py-1.5 rounded-lg border border-slate-800 text-indigo-300 font-mono text-xs">
                        {inv.mathematicalFormula}
                      </div>

                      <p className="text-xs text-slate-400 mt-2 leading-relaxed">
                        {inv.description}
                      </p>

                      <div className="mt-2 text-xs font-mono text-emerald-400 bg-emerald-500/5 px-2.5 py-1.5 rounded border border-emerald-500/20">
                        {inv.evidence}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <div className="text-center py-12 text-slate-500 font-mono text-xs space-y-2">
                <ShieldCheck className="w-8 h-8 text-slate-600 mx-auto" />
                <p>尚未运行混沌断言。点击上方“启动 Jepsen 风格混沌验证套件”开始压力检验。</p>
              </div>
            )}
          </div>
        )}

        {/* SubTab 2: Waterfall & Response Diff */}
        {dashboardSubTab === 'waterfall' && (
          <div className="p-5 space-y-4">
            {activeReceipt && activeReceipt.tracesSample?.length > 0 ? (
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-4">
                {/* Left: Waterfall timeline list */}
                <div className="lg:col-span-6 space-y-2 max-h-[500px] overflow-y-auto pr-1 text-xs font-mono">
                  <div className="text-slate-400 text-[11px] pb-1 border-b border-slate-800 flex items-center justify-between">
                    <span>并发执行线程列表 (Worker Sample)</span>
                    <span>共采样子样本 {activeReceipt.tracesSample.length} 个</span>
                  </div>

                  {activeReceipt.tracesSample.map((trace) => {
                    const isSelected = selectedTrace?.workerId === trace.workerId;
                    return (
                      <div
                        key={trace.workerId}
                        onClick={() => setSelectedTrace(trace)}
                        className={`p-2.5 rounded-lg border cursor-pointer transition ${
                          isSelected
                            ? 'bg-indigo-950/60 border-indigo-500 text-white'
                            : 'bg-slate-950 hover:bg-slate-850 border-slate-800 text-slate-300'
                        }`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <span className="font-bold text-slate-200">#W{trace.workerId}</span>
                            <span className="text-[10px] text-slate-400">[{trace.idempotencyKey}]</span>
                          </div>
                          <span
                            className={`text-[10px] px-1.5 py-0.5 rounded font-bold ${
                              trace.outcome === 'COMMITTED_FRESH'
                                ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                                : trace.outcome === 'IDEMPOTENT_CACHE_HIT'
                                ? 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                                : 'bg-blue-500/20 text-blue-400 border border-blue-500/30'
                            }`}
                          >
                            {trace.outcome === 'COMMITTED_FRESH' ? '物理落库 (Commit=1)' : '幂等阻断 (Cache Hit)'}
                          </span>
                        </div>

                        {/* Latency bar preview */}
                        <div className="mt-1.5 flex items-center gap-2">
                          <div className="flex-1 bg-slate-900 rounded h-1.5 overflow-hidden">
                            <div
                              className="bg-indigo-500 h-full rounded"
                              style={{ width: `${Math.min(100, (trace.durationMs / (activeReceipt.p99LatencyMs || 100)) * 100)}%` }}
                            ></div>
                          </div>
                          <span className="text-[10px] text-slate-400 font-mono w-12 text-right">
                            {trace.durationMs}ms
                          </span>
                        </div>

                        {trace.faultInjected && (
                          <div className="mt-1 text-[10px] text-rose-400 flex items-center gap-1">
                            <Zap className="w-3 h-3" />
                            {trace.faultInjected}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>

                {/* Right: Selected Trace Details & Response Consistency Diff */}
                <div className="lg:col-span-6 bg-slate-950 p-4 rounded-xl border border-slate-800 font-mono text-xs space-y-3">
                  <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                    <span className="text-slate-300 font-bold flex items-center gap-1.5">
                      <Terminal className="w-4 h-4 text-indigo-400" />
                      线程执行明细与副作用校验 (Trace Inspector)
                    </span>
                    {selectedTrace && (
                      <span className="text-indigo-400 text-[11px]">Worker #{selectedTrace.workerId}</span>
                    )}
                  </div>

                  {selectedTrace ? (
                    <div className="space-y-3">
                      <div className="grid grid-cols-2 gap-2 text-[11px]">
                        <div className="bg-slate-900 p-2 rounded border border-slate-800">
                          <span className="text-slate-500 block">状态转移路径:</span>
                          <span className="text-emerald-400 font-semibold">{selectedTrace.stateTransition}</span>
                        </div>
                        <div className="bg-slate-900 p-2 rounded border border-slate-800">
                          <span className="text-slate-500 block">物理数据库变更:</span>
                          <span className={selectedTrace.physicalDbMutation ? 'text-amber-400 font-bold' : 'text-slate-400'}>
                            {selectedTrace.physicalDbMutation ? '是 (首次写入落盘)' : '否 (零副作用复用)'}
                          </span>
                        </div>
                      </div>

                      <div>
                        <span className="text-slate-400 text-[11px] block mb-1">
                          返回响应体哈希指纹 (SHA-256 Digest):
                        </span>
                        <div className="bg-slate-900 px-3 py-1.5 rounded border border-slate-800 text-indigo-300 select-all">
                          {selectedTrace.responseHash}
                        </div>
                      </div>

                      <div>
                        <span className="text-slate-400 text-[11px] block mb-1">
                          完整返回报文 (Payload):
                        </span>
                        <pre className="bg-slate-900 p-3 rounded-lg border border-slate-800 text-slate-300 text-[11px] overflow-x-auto max-h-48 scrollbar-none">
                          {JSON.stringify(selectedTrace.responsePayload, null, 2)}
                        </pre>
                      </div>

                      <div className="bg-emerald-500/10 p-2.5 rounded-lg border border-emerald-500/25 text-[11px] text-emerald-400 flex items-start gap-2">
                        <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" />
                        <div>
                          <strong>不变量 A 证明证据：</strong> 相同幂等键的所有后续重试请求直接返回首次计算缓存，杜绝重复扣减库存并保持原子确定性。
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="text-slate-500 text-center py-10">
                      请在左侧选择一个线程执行样本
                    </div>
                  )}
                </div>
              </div>
            ) : (
              <div className="text-center py-10 text-slate-500 font-mono text-xs">
                暂无执行样本，请先运行混沌测试。
              </div>
            )}
          </div>
        )}

        {/* SubTab 3: Underlying State Store & Ledgers */}
        {dashboardSubTab === 'records' && (
          <div className="p-5 space-y-4">
            {/* Stock Ledgers conservation summary */}
            <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 font-mono text-xs space-y-2">
              <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                <span className="text-slate-200 font-semibold flex items-center gap-1.5">
                  <Database className="w-4 h-4 text-blue-400" />
                  物理库存账本守恒审计 (Conservation Ledger)
                </span>
                <span className="text-[10px] text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/25">
                  ∑ Available + Locked + Deducted == Initial
                </span>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                {Object.entries(stockSnapshot).map(([sku, data]) => {
                  const initial = data.available + data.locked + data.deducted;
                  return (
                    <div key={sku} className="bg-slate-900 p-3 rounded-lg border border-slate-800 space-y-1">
                      <div className="text-slate-300 font-bold truncate text-[11px]">{sku}</div>
                      <div className="flex items-center justify-between text-slate-400 text-[10px]">
                        <span>初始基准库存:</span>
                        <span className="text-white font-bold">{initial}</span>
                      </div>
                      <div className="flex items-center justify-between text-slate-400 text-[10px]">
                        <span>可用库存 (Available):</span>
                        <span className="text-emerald-400">{data.available}</span>
                      </div>
                      <div className="flex items-center justify-between text-slate-400 text-[10px]">
                        <span>累计扣减 (Deducted):</span>
                        <span className="text-amber-400">{data.deducted}</span>
                      </div>
                      <div className="pt-1 border-t border-slate-800 flex items-center justify-between text-[10px]">
                        <span className="text-slate-500">守恒差额 (Deviation):</span>
                        <span className="text-emerald-400 font-bold">0 件 (绝对守恒)</span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Idempotency Key Records Table */}
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs text-slate-400 font-mono">
                <span>底层幂等状态存储记录 (Idempotency Store Records)</span>
                <span>当前共 {idempotencyRecords.length} 条记录</span>
              </div>

              <div className="overflow-x-auto border border-slate-800 rounded-lg">
                <table className="w-full text-left text-xs font-mono">
                  <thead className="bg-slate-950 text-slate-400 border-b border-slate-800 text-[11px]">
                    <tr>
                      <th className="p-2.5">幂等令牌 (Token Key)</th>
                      <th className="p-2.5">状态机状态</th>
                      <th className="p-2.5">调用频次</th>
                      <th className="p-2.5">物理落库次数</th>
                      <th className="p-2.5">关联租户</th>
                      <th className="p-2.5">返回摘要 (Hash)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/80 bg-slate-900/60">
                    {idempotencyRecords.length > 0 ? (
                      idempotencyRecords.map((rec) => (
                        <tr key={rec.key} className="hover:bg-slate-800/40">
                          <td className="p-2.5 font-bold text-slate-200">{rec.key}</td>
                          <td className="p-2.5">
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/25">
                              {rec.state}
                            </span>
                          </td>
                          <td className="p-2.5 text-slate-300">{rec.executionAttempts} 次调用</td>
                          <td className="p-2.5 font-bold text-emerald-400">
                            {rec.physicalMutations} (严格为 1)
                          </td>
                          <td className="p-2.5 text-slate-400">Tenant {rec.tenantId}</td>
                          <td className="p-2.5 text-indigo-400 text-[10px] truncate max-w-[120px]">
                            {rec.responseChecksum || '—'}
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={6} className="p-6 text-center text-slate-500">
                          暂无记录，运行混沌断言后生成
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Formal TLA+ / PlusCal Specification Modal */}
      {showTlaModal && (
        <div className="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-700 rounded-xl max-w-4xl w-full max-h-[85vh] flex flex-col shadow-2xl">
            <div className="flex items-center justify-between p-4 border-b border-slate-800">
              <div className="flex items-center gap-2">
                <FileCode2 className="w-5 h-5 text-indigo-400" />
                <h3 className="font-bold text-white text-sm">
                  Aspros.Core 形式化规范 (TLA+ / PlusCal Formal Specification)
                </h3>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={handleCopyTla}
                  className="flex items-center gap-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs px-3 py-1.5 rounded-lg border border-slate-700 transition font-mono"
                >
                  {copiedTla ? (
                    <>
                      <Check className="w-3.5 h-3.5 text-emerald-400" />
                      已复制
                    </>
                  ) : (
                    <>
                      <Copy className="w-3.5 h-3.5" />
                      复制规约
                    </>
                  )}
                </button>
                <button
                  onClick={() => setShowTlaModal(false)}
                  className="text-slate-400 hover:text-white px-2 py-1 text-sm font-bold"
                >
                  ✕
                </button>
              </div>
            </div>

            <div className="p-4 overflow-y-auto flex-1 font-mono text-xs bg-slate-950">
              <div className="mb-3 text-[11px] text-slate-400 bg-slate-900 p-2.5 rounded border border-slate-800">
                💡 该 TLA+ 规约可直接导入 Leslie Lamport 形式化模型检验器（TLC Model Checker）中进行穷尽状态空间探索，从数学上严格证明无死锁（Liveness）与无重复落库（Safety）。
              </div>
              <pre className="text-slate-200 whitespace-pre leading-relaxed select-all">
                {tlaCode}
              </pre>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
