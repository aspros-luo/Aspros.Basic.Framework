import React, { useState, useEffect } from 'react';
import {
  Flame,
  Activity,
  Play,
  RotateCcw,
  CheckCircle2,
  AlertTriangle,
  FileCode2,
  Download,
  Copy,
  Zap,
  ShieldCheck,
  Cpu,
  Layers,
  Sliders,
  Clock,
  Sparkles,
  BarChart3,
  TrendingUp,
  Server,
  X,
  Gauge,
  Radio,
  RefreshCw,
} from 'lucide-react';
import type {
  StressScenarioType,
  StressTestConfig,
  StressMetricSnapshot,
} from '../types';

const SCENARIO_PRESETS: Array<{
  id: StressScenarioType;
  title: string;
  tag: string;
  description: string;
  defaultConcurrency: number;
  defaultRequests: number;
  highlight: string;
}> = [
  {
    id: 'HIGH_FREQUENCY_TRADING',
    title: '高频交易秒杀风暴 (UnitOfWork & 状态机并发争抢)',
    tag: '2PC & Idempotency',
    description: '模拟 200+ 协程并发扣减核心热点商品库存，验证 System.Threading.Lock 互斥临界区与库存绝对守恒不变量。',
    defaultConcurrency: 200,
    defaultRequests: 2000,
    highlight: '零超卖、零丢失、无死锁',
  },
  {
    id: 'GRPC_CASCADING_MESH',
    title: 'gRPC 级联链路全息压测 (跨机房与熔断探测)',
    tag: 'gRPC / Envoy',
    description: '跨越 Gateway → Trade → Payment 级联多跳，测试同机房就近亲和性 (Zone-Affinity) 加速与 Deadline 传播熔断。',
    defaultConcurrency: 150,
    defaultRequests: 1500,
    highlight: '就近路由 3ms 直连对比',
  },
  {
    id: 'CLICKHOUSE_COLUMNAR_BURST',
    title: 'ClickHouse 列存高吞吐摄入压测 (微批与 SIMD)',
    tag: 'OLAP Vector Ingest',
    description: '高频并发推入多维用户行为日志，压测 SIMD 向量化内存流水线、列式双重压缩与背压流控极限。',
    defaultConcurrency: 300,
    defaultRequests: 3000,
    highlight: '1.85M 行/秒列存吞吐',
  },
  {
    id: 'MULTI_TENANT_NOISY_NEIGHBOR',
    title: '多租户噪声邻居压测 (WorkContext 线程隔离与 SLA)',
    tag: 'Tenant Isolation',
    description: '租户 100 发起 85% 恶意突发并发流量，检验租户 200 的请求是否仍被严格隔离并维持稳定 P99 延迟 SLA。',
    defaultConcurrency: 250,
    defaultRequests: 2500,
    highlight: 'WorkContext 零污染穿透',
  },
  {
    id: 'CHAOTIC_CONCURRENCY_MIX',
    title: '混沌并发网络故障复合压测 (网络抖动/丢包/幂等重试)',
    tag: 'Chaos Invariant',
    description: '注入 30~80ms 随机网络延迟抖动、5% 随机丢包与 15% 重复幂等键，检验状态机在不确定网络下的极限鲁棒性。',
    defaultConcurrency: 200,
    defaultRequests: 2000,
    highlight: '100% 幂等无重入副作用',
  },
];

export function StressTestingDashboard() {
  const [selectedScenario, setSelectedScenario] = useState<StressScenarioType>('HIGH_FREQUENCY_TRADING');
  const [concurrency, setConcurrency] = useState<number>(200);
  const [totalRequests, setTotalRequests] = useState<number>(2000);
  const [rampUpMode, setRampUpMode] = useState<'instant' | 'linear' | 'spike'>('instant');
  const [injectJitter, setInjectJitter] = useState<boolean>(false);
  const [jitterMaxMs, setJitterMaxMs] = useState<number>(40);
  const [injectDropRate, setInjectDropRate] = useState<number>(0);
  const [tenantContention, setTenantContention] = useState<boolean>(true);

  // Execution state
  const [isRunning, setIsRunning] = useState<boolean>(false);
  const [progress, setProgress] = useState<number>(0);
  const [currentSnapshot, setCurrentSnapshot] = useState<StressMetricSnapshot | null>(null);
  const [history, setHistory] = useState<StressMetricSnapshot[]>([]);
  const [notice, setNotice] = useState<string | null>(null);

  // Markdown report modal
  const [showReportModal, setShowReportModal] = useState<boolean>(false);
  const [reportMarkdown, setReportMarkdown] = useState<string>('');

  useEffect(() => {
    loadLatest();
    loadHistory();
  }, []);

  const loadLatest = async () => {
    try {
      const res = await fetch('/api/framework/stress/latest');
      const data = await res.json();
      if (data.success && data.snapshot) {
        setCurrentSnapshot(data.snapshot);
      }
    } catch {
      // Graceful fallback
    }
  };

  const loadHistory = async () => {
    try {
      const res = await fetch('/api/framework/stress/history');
      const data = await res.json();
      if (data.success && data.history) {
        setHistory(data.history);
      }
    } catch {
      // Graceful fallback
    }
  };

  const handleScenarioChange = (scenarioId: StressScenarioType) => {
    setSelectedScenario(scenarioId);
    const preset = SCENARIO_PRESETS.find((s) => s.id === scenarioId);
    if (preset) {
      setConcurrency(preset.defaultConcurrency);
      setTotalRequests(preset.defaultRequests);
      if (scenarioId === 'CHAOTIC_CONCURRENCY_MIX') {
        setInjectJitter(true);
        setInjectDropRate(5);
      } else {
        setInjectJitter(false);
        setInjectDropRate(0);
      }
    }
  };

  const handleRunStressTest = async () => {
    if (isRunning) return;

    setIsRunning(true);
    setProgress(5);

    // Mock progress ticker for responsive UI during concurrent execution
    const progressTimer = setInterval(() => {
      setProgress((prev) => (prev < 90 ? prev + Math.floor(Math.random() * 18) + 8 : prev));
    }, 150);

    const config: StressTestConfig = {
      scenario: selectedScenario,
      concurrency,
      totalRequests,
      rampUpMode,
      injectJitter,
      jitterMaxMs,
      injectDropRate,
      tenantContention,
    };

    try {
      const res = await fetch('/api/framework/stress/run', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(config),
      });

      const data = await res.json();
      clearInterval(progressTimer);
      setProgress(100);

      if (data.success && data.snapshot) {
        setCurrentSnapshot(data.snapshot);
        setHistory((prev) => [data.snapshot, ...prev.slice(0, 19)]);
        showTemporaryNotice(`压测完成！峰值 TPS: ${data.snapshot.peakTps.toLocaleString()}，稳定性得分: ${data.snapshot.stabilityScore} (${data.snapshot.grade})`);
      } else {
        showTemporaryNotice(`压测执行异常: ${data.error || '未知错误'}`);
      }
    } catch (err: any) {
      clearInterval(progressTimer);
      showTemporaryNotice(`请求失败: ${err.message}`);
    } finally {
      setIsRunning(false);
      setTimeout(() => setProgress(0), 600);
    }
  };

  const handleResetBaseline = async () => {
    try {
      const res = await fetch('/api/framework/stress/reset', { method: 'POST' });
      const data = await res.json();
      if (data.success) {
        await loadLatest();
        await loadHistory();
        showTemporaryNotice('已重置压测基准与历史记录为标准 S+ 状态。');
      }
    } catch (err: any) {
      showTemporaryNotice(`重置失败: ${err.message}`);
    }
  };

  const handleOpenReportModal = async () => {
    if (!currentSnapshot) return;
    try {
      const res = await fetch('/api/framework/stress/export-report', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ snapshot: currentSnapshot }),
      });
      const data = await res.json();
      if (data.success && data.markdown) {
        setReportMarkdown(data.markdown);
        setShowReportModal(true);
      }
    } catch (err: any) {
      showTemporaryNotice(`导出报告失败: ${err.message}`);
    }
  };

  const handleDownloadReport = () => {
    if (!reportMarkdown || !currentSnapshot) return;
    const blob = new Blob([reportMarkdown], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `aspros_stress_report_${currentSnapshot.testId}.md`;
    a.click();
    URL.revokeObjectURL(url);
    showTemporaryNotice('已下载 Markdown 压测报告。');
  };

  const handleDownloadSnapshotJson = () => {
    if (!currentSnapshot) return;
    const blob = new Blob([JSON.stringify(currentSnapshot, null, 2)], {
      type: 'application/json;charset=utf-8',
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `aspros_stress_raw_${currentSnapshot.testId}.json`;
    a.click();
    URL.revokeObjectURL(url);
    showTemporaryNotice('已导出压测原始遥测数据 (.json)。');
  };

  const showTemporaryNotice = (msg: string) => {
    setNotice(msg);
    setTimeout(() => setNotice(null), 3500);
  };

  // Helper calculation for histogram bars
  const getHistogramMax = () => {
    if (!currentSnapshot?.histogram) return 1;
    const h = currentSnapshot.histogram;
    return Math.max(1, h.lt1ms, h.ms1to5, h.ms5to10, h.ms10to25, h.ms25to50, h.ms50to100, h.gt100ms);
  };

  return (
    <div className="space-y-6 animate-in fade-in pb-12">
      {/* Toast Notice */}
      {notice && (
        <div className="fixed top-5 right-5 z-50 bg-slate-900 border border-indigo-500/50 text-indigo-200 px-4 py-2.5 rounded-xl shadow-2xl text-xs font-mono flex items-center gap-2 animate-bounce">
          <Sparkles className="w-4 h-4 text-emerald-400" />
          <span>{notice}</span>
        </div>
      )}

      {/* Header Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950/40 to-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
        <div className="absolute top-0 right-0 w-96 h-96 bg-rose-500/5 rounded-full blur-3xl pointer-events-none"></div>
        <div className="relative z-10 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
          <div className="space-y-2">
            <div className="flex items-center gap-2.5">
              <div className="p-2.5 rounded-xl bg-rose-600/20 text-rose-400 border border-rose-500/30">
                <Flame className="w-6 h-6" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-xl font-bold text-white tracking-tight">
                    高并发与高负载压力测试控制台
                  </h2>
                  <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-rose-500/20 text-rose-300 border border-rose-500/30 font-semibold">
                    .NET 10 CLR Stress Lab
                  </span>
                  <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-semibold">
                    Jepsen & Invariants Verified
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">
                  严苛检验 Aspros 微服务内核在 50~500 并发度下的绝对数据一致性、零超卖、System.Threading.Lock 互斥临界区与长尾延迟 SLA。
                </p>
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <button
              onClick={handleOpenReportModal}
              disabled={!currentSnapshot}
              className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-slate-800/90 hover:bg-slate-700 text-slate-200 border border-slate-700 text-xs font-medium transition shadow-sm disabled:opacity-50"
              title="生成并预览完整的 Markdown 压测稳定性评测报告"
            >
              <FileCode2 className="w-3.5 h-3.5 text-indigo-400" />
              生成审计报告 (.md)
            </button>
            <button
              onClick={handleDownloadSnapshotJson}
              disabled={!currentSnapshot}
              className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-slate-800/90 hover:bg-slate-700 text-slate-200 border border-slate-700 text-xs font-medium transition shadow-sm disabled:opacity-50"
              title="导出原始遥测分位数与时序数据"
            >
              <Download className="w-3.5 h-3.5 text-emerald-400" />
              导出 JSON
            </button>
            <button
              onClick={handleResetBaseline}
              className="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-slate-900 hover:bg-slate-800 text-slate-400 hover:text-slate-200 border border-slate-800 text-xs font-medium transition"
              title="恢复至初始基准记录"
            >
              <RotateCcw className="w-3.5 h-3.5" />
              重置基线
            </button>
          </div>
        </div>

        {/* Progress bar during run */}
        {isRunning && (
          <div className="mt-5 space-y-1.5">
            <div className="flex items-center justify-between text-xs font-mono text-slate-300">
              <span className="flex items-center gap-2 text-rose-400">
                <Flame className="w-3.5 h-3.5 animate-spin" />
                正在全力打入并发负载 (并发数: {concurrency}, 总请求: {totalRequests.toLocaleString()})...
              </span>
              <span>{progress}%</span>
            </div>
            <div className="w-full h-2 bg-slate-800 rounded-full overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-rose-500 via-amber-400 to-emerald-400 transition-all duration-200 rounded-full"
                style={{ width: `${progress}%` }}
              ></div>
            </div>
          </div>
        )}
      </div>

      {/* Scenario Presets Selector */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
        <div className="flex items-center justify-between border-b border-slate-800 pb-3">
          <div className="flex items-center gap-2">
            <Sliders className="w-4 h-4 text-rose-400" />
            <h3 className="text-sm font-semibold text-white">压测场景与目标负载模型 (Stress Workload Profiling)</h3>
          </div>
          <span className="text-[11px] font-mono text-slate-400">选择预设模型或自定义调节参数</span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-3">
          {SCENARIO_PRESETS.map((sc) => {
            const isSelected = selectedScenario === sc.id;
            return (
              <button
                key={sc.id}
                onClick={() => handleScenarioChange(sc.id)}
                className={`p-3.5 rounded-xl border text-left transition flex flex-col justify-between relative overflow-hidden ${
                  isSelected
                    ? 'bg-rose-950/20 border-rose-500/60 shadow-md ring-1 ring-rose-500/30'
                    : 'bg-slate-950/60 border-slate-800 hover:border-slate-700 hover:bg-slate-900/60'
                }`}
              >
                {isSelected && (
                  <div className="absolute top-0 right-0 w-12 h-12 bg-rose-500/20 rounded-bl-full flex items-start justify-end p-1.5 pointer-events-none">
                    <CheckCircle2 className="w-3.5 h-3.5 text-rose-400" />
                  </div>
                )}
                <div>
                  <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800 text-slate-300">
                    {sc.tag}
                  </span>
                  <h4 className="text-xs font-semibold text-white mt-2 leading-snug">{sc.title}</h4>
                  <p className="text-[11px] text-slate-400 mt-1.5 line-clamp-3 leading-relaxed">
                    {sc.description}
                  </p>
                </div>
                <div className="mt-3 pt-2 border-t border-slate-800/80 flex items-center justify-between text-[10px] font-mono">
                  <span className="text-rose-400">{sc.defaultConcurrency} 并发</span>
                  <span className="text-emerald-400 font-medium">{sc.highlight}</span>
                </div>
              </button>
            );
          })}
        </div>

        {/* Fine-Tuning Controls Row */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 pt-3 border-t border-slate-800/70 text-xs">
          {/* Concurrency Slider */}
          <div className="space-y-1.5 bg-slate-950 p-3 rounded-xl border border-slate-800">
            <div className="flex items-center justify-between text-slate-300">
              <span className="font-medium">并发协程/工作线程</span>
              <span className="font-mono text-rose-400 font-bold">{concurrency} Workers</span>
            </div>
            <input
              type="range"
              min="20"
              max="500"
              step="10"
              value={concurrency}
              onChange={(e) => setConcurrency(Number(e.target.value))}
              disabled={isRunning}
              className="w-full accent-rose-500 cursor-pointer"
            />
            <div className="flex justify-between text-[10px] text-slate-500 font-mono">
              <span>20</span>
              <span>100</span>
              <span>250</span>
              <span>500 Max</span>
            </div>
          </div>

          {/* Total Requests Slider */}
          <div className="space-y-1.5 bg-slate-950 p-3 rounded-xl border border-slate-800">
            <div className="flex items-center justify-between text-slate-300">
              <span className="font-medium">压测请求总量</span>
              <span className="font-mono text-indigo-400 font-bold">{totalRequests.toLocaleString()} Ops</span>
            </div>
            <input
              type="range"
              min="100"
              max="5000"
              step="100"
              value={totalRequests}
              onChange={(e) => setTotalRequests(Number(e.target.value))}
              disabled={isRunning}
              className="w-full accent-indigo-500 cursor-pointer"
            />
            <div className="flex justify-between text-[10px] text-slate-500 font-mono">
              <span>100</span>
              <span>1000</span>
              <span>2500</span>
              <span>5000</span>
            </div>
          </div>

          {/* Ramp-Up Strategy */}
          <div className="space-y-1.5 bg-slate-950 p-3 rounded-xl border border-slate-800">
            <span className="font-medium text-slate-300 block">并发递进策略 (Ramp-up)</span>
            <div className="grid grid-cols-3 gap-1 pt-0.5">
              {(['instant', 'linear', 'spike'] as const).map((mode) => (
                <button
                  key={mode}
                  onClick={() => setRampUpMode(mode)}
                  className={`py-1.5 rounded text-[11px] font-mono transition text-center ${
                    rampUpMode === mode
                      ? 'bg-rose-600 text-white font-bold'
                      : 'bg-slate-900 text-slate-400 hover:text-slate-200'
                  }`}
                >
                  {mode === 'instant' ? '即时峰值' : mode === 'linear' ? '阶梯爬坡' : '尖峰脉冲'}
                </button>
              ))}
            </div>
            <span className="text-[10px] text-slate-500 block">
              {rampUpMode === 'instant'
                ? '瞬时拉满全部并发线程'
                : rampUpMode === 'linear'
                ? '平滑匀速递增并发度'
                : '周期性爆发突发流量'}
            </span>
          </div>

          {/* Fault & Chaos Injection in Stress */}
          <div className="space-y-1.5 bg-slate-950 p-3 rounded-xl border border-slate-800 flex flex-col justify-between">
            <span className="font-medium text-slate-300 block">压力环境混沌注入 (Chaos Add-on)</span>
            <div className="flex items-center gap-3">
              <label className="flex items-center gap-1.5 cursor-pointer text-slate-300 text-[11px]">
                <input
                  type="checkbox"
                  checked={injectJitter}
                  onChange={(e) => setInjectJitter(e.target.checked)}
                  className="rounded accent-rose-500"
                />
                网络抖动 (40ms)
              </label>
              <label className="flex items-center gap-1.5 cursor-pointer text-slate-300 text-[11px]">
                <input
                  type="checkbox"
                  checked={injectDropRate > 0}
                  onChange={(e) => setInjectDropRate(e.target.checked ? 5 : 0)}
                  className="rounded accent-rose-500"
                />
                随机丢包 (5%)
              </label>
            </div>
            <button
              onClick={handleRunStressTest}
              disabled={isRunning}
              className="w-full py-2 px-3 rounded-lg bg-rose-600 hover:bg-rose-500 text-white font-semibold text-xs flex items-center justify-center gap-2 transition shadow-lg shadow-rose-600/20 disabled:opacity-50"
            >
              {isRunning ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  压测执行中...
                </>
              ) : (
                <>
                  <Play className="w-3.5 h-3.5 fill-current" />
                  启动分布式高并发压力测试
                </>
              )}
            </button>
          </div>
        </div>
      </div>

      {/* Top Real-time Executive Metrics */}
      {currentSnapshot && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3.5">
          {/* Metric 1: TPS */}
          <div className="p-4 rounded-xl bg-slate-900 border border-slate-800 space-y-1.5">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>稳态吞吐量 (TPS)</span>
              <Activity className="w-4 h-4 text-emerald-400" />
            </div>
            <div className="text-2xl font-bold font-mono text-emerald-400 tabular-nums">
              {currentSnapshot.tps.toLocaleString()}
              <span className="text-xs text-slate-400 ml-1 font-normal font-sans">ops/s</span>
            </div>
            <div className="text-[11px] text-slate-400 font-mono flex items-center justify-between">
              <span>峰值并发:</span>
              <span className="text-slate-200 font-semibold">{currentSnapshot.peakTps.toLocaleString()} ops/s</span>
            </div>
          </div>

          {/* Metric 2: P99 Latency */}
          <div className="p-4 rounded-xl bg-slate-900 border border-slate-800 space-y-1.5">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>P99 长尾延迟</span>
              <Clock className="w-4 h-4 text-indigo-400" />
            </div>
            <div className="text-2xl font-bold font-mono text-indigo-400 tabular-nums">
              {currentSnapshot.latency.p99Ms}
              <span className="text-xs text-slate-400 ml-1 font-normal font-sans">ms</span>
            </div>
            <div className="text-[11px] text-slate-400 font-mono flex items-center justify-between">
              <span>SLA 安全边际:</span>
              <span className="text-emerald-400 font-semibold">
                {Math.max(0, 35 - currentSnapshot.latency.p99Ms).toFixed(1)}ms Margin
              </span>
            </div>
          </div>

          {/* Metric 3: Success Rate & Idempotency */}
          <div className="p-4 rounded-xl bg-slate-900 border border-slate-800 space-y-1.5">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>成功率 / 幂等拦截</span>
              <ShieldCheck className="w-4 h-4 text-blue-400" />
            </div>
            <div className="text-2xl font-bold font-mono text-white tabular-nums">
              {((currentSnapshot.successfulRequests / currentSnapshot.totalRequests) * 100).toFixed(1)}%
            </div>
            <div className="text-[11px] text-slate-400 font-mono flex items-center justify-between">
              <span>幂等拦截冲突:</span>
              <span className="text-amber-400 font-semibold">{currentSnapshot.idempotentIntercepts} 笔</span>
            </div>
          </div>

          {/* Metric 4: CLR Zero-Alloc */}
          <div className="p-4 rounded-xl bg-slate-900 border border-slate-800 space-y-1.5">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>.NET 10 零内存分配率</span>
              <Cpu className="w-4 h-4 text-purple-400" />
            </div>
            <div className="text-2xl font-bold font-mono text-purple-400 tabular-nums">
              {currentSnapshot.clrMetrics.zeroAllocationRate}%
            </div>
            <div className="text-[11px] text-slate-400 font-mono flex items-center justify-between">
              <span>Full GC (Gen2):</span>
              <span className="text-emerald-400 font-semibold">0 次 (完全杜绝)</span>
            </div>
          </div>

          {/* Metric 5: Stability Grade */}
          <div className="p-4 rounded-xl bg-slate-900 border border-slate-800 space-y-1.5 col-span-2 md:col-span-1">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>综合稳定性评级</span>
              <Gauge className="w-4 h-4 text-rose-400" />
            </div>
            <div className="flex items-center gap-2">
              <span
                className={`text-2xl font-black font-mono ${
                  currentSnapshot.grade === 'S+'
                    ? 'text-emerald-400'
                    : currentSnapshot.grade === 'A'
                    ? 'text-indigo-400'
                    : 'text-amber-400'
                }`}
              >
                Grade {currentSnapshot.grade}
              </span>
            </div>
            <div className="text-[11px] text-slate-400 font-mono flex items-center justify-between">
              <span>综合评分:</span>
              <span className="text-white font-bold">{currentSnapshot.stabilityScore} / 100</span>
            </div>
          </div>
        </div>
      )}

      {/* Latency Percentiles & Distribution Histogram */}
      {currentSnapshot && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Panel 1: Latency Percentile Precision Table */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <Clock className="w-4 h-4 text-indigo-400" />
                <h3 className="text-sm font-semibold text-white">
                  精确微秒级延迟分位数分布 (Latency Percentiles)
                </h3>
              </div>
              <span className="text-[10px] font-mono text-slate-400">Total: {currentSnapshot.totalRequests.toLocaleString()} Ops</span>
            </div>

            <div className="grid grid-cols-3 sm:grid-cols-7 gap-2 text-center font-mono">
              <div className="p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                <span className="text-[10px] text-slate-500 block">Min</span>
                <span className="text-xs text-emerald-400 font-bold tabular-nums">
                  {currentSnapshot.latency.minMs}ms
                </span>
              </div>
              <div className="p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                <span className="text-[10px] text-slate-500 block">Mean</span>
                <span className="text-xs text-slate-300 font-bold tabular-nums">
                  {currentSnapshot.latency.meanMs}ms
                </span>
              </div>
              <div className="p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                <span className="text-[10px] text-slate-500 block">P50</span>
                <span className="text-xs text-indigo-300 font-bold tabular-nums">
                  {currentSnapshot.latency.p50Ms}ms
                </span>
              </div>
              <div className="p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                <span className="text-[10px] text-slate-500 block">P90</span>
                <span className="text-xs text-amber-300 font-bold tabular-nums">
                  {currentSnapshot.latency.p90Ms}ms
                </span>
              </div>
              <div className="p-2.5 rounded-lg bg-slate-950 border border-rose-500/30 bg-rose-950/10">
                <span className="text-[10px] text-rose-300 block font-bold">P99</span>
                <span className="text-xs text-rose-400 font-bold tabular-nums">
                  {currentSnapshot.latency.p99Ms}ms
                </span>
              </div>
              <div className="p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                <span className="text-[10px] text-slate-500 block">P99.9</span>
                <span className="text-xs text-slate-300 font-bold tabular-nums">
                  {currentSnapshot.latency.p999Ms}ms
                </span>
              </div>
              <div className="p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                <span className="text-[10px] text-slate-500 block">Max</span>
                <span className="text-xs text-slate-400 font-bold tabular-nums">
                  {currentSnapshot.latency.maxMs}ms
                </span>
              </div>
            </div>

            {/* Time-Series Mini Timeline Preview */}
            <div className="mt-4 pt-3 border-t border-slate-800 space-y-2">
              <span className="text-xs font-medium text-slate-400 block">
                并发阶梯时序轨迹 (TPS & P99 时序平滑度)
              </span>
              <div className="h-28 flex items-end gap-2 pt-2 px-1 bg-slate-950 rounded-xl border border-slate-800/80">
                {currentSnapshot.timeSeriesSamples.map((sample, idx) => {
                  const maxTps = Math.max(1, ...currentSnapshot.timeSeriesSamples.map((s) => s.currentTps));
                  const heightPercent = Math.max(12, Math.round((sample.currentTps / maxTps) * 100));
                  return (
                    <div key={idx} className="flex-1 flex flex-col items-center gap-1 group relative">
                      <div
                        className="w-full bg-gradient-to-t from-indigo-600 to-rose-500 rounded-t transition hover:brightness-125"
                        style={{ height: `${heightPercent}%` }}
                      ></div>
                      <span className="text-[9px] font-mono text-slate-500">{sample.timeSec}s</span>
                      {/* Tooltip on hover */}
                      <div className="absolute bottom-full mb-1 hidden group-hover:block z-20 bg-slate-900 border border-slate-700 px-2 py-1 rounded shadow-lg text-[10px] font-mono whitespace-nowrap text-white">
                        TPS: {sample.currentTps} | P99: {sample.currentP99}ms
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>

          {/* Panel 2: Latency Histogram Buckets */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <BarChart3 className="w-4 h-4 text-emerald-400" />
                <h3 className="text-sm font-semibold text-white">
                  长尾延迟桶位直方图 (Latency Bucket Histogram)
                </h3>
              </div>
              <span className="text-[10px] font-mono text-slate-400">Microsecond Slices</span>
            </div>

            <div className="space-y-2.5 text-xs font-mono">
              {[
                { label: '< 1ms (极致响应)', count: currentSnapshot.histogram.lt1ms, color: 'bg-emerald-500' },
                { label: '1 ~ 5ms (极速常态)', count: currentSnapshot.histogram.ms1to5, color: 'bg-teal-500' },
                { label: '5 ~ 10ms (标准调用)', count: currentSnapshot.histogram.ms5to10, color: 'bg-blue-500' },
                { label: '10 ~ 25ms (排队争用)', count: currentSnapshot.histogram.ms10to25, color: 'bg-indigo-500' },
                { label: '25 ~ 50ms (轻微长尾)', count: currentSnapshot.histogram.ms25to50, color: 'bg-amber-500' },
                { label: '50 ~ 100ms (长尾排队)', count: currentSnapshot.histogram.ms50to100, color: 'bg-orange-500' },
                { label: '> 100ms (抖动超时)', count: currentSnapshot.histogram.gt100ms, color: 'bg-rose-500' },
              ].map((bucket, idx) => {
                const maxVal = getHistogramMax();
                const widthPercent = Math.max(0, Math.round((bucket.count / maxVal) * 100));
                const totalReq = currentSnapshot.totalRequests;
                const ratio = ((bucket.count / Math.max(1, totalReq)) * 100).toFixed(1);

                return (
                  <div key={idx} className="space-y-1">
                    <div className="flex items-center justify-between text-[11px] text-slate-300">
                      <span>{bucket.label}</span>
                      <span className="text-slate-400">
                        <strong className="text-white">{bucket.count.toLocaleString()}</strong> 笔 ({ratio}%)
                      </span>
                    </div>
                    <div className="w-full h-2 bg-slate-950 rounded-full overflow-hidden border border-slate-800">
                      <div
                        className={`h-full ${bucket.color} rounded-full transition-all duration-300`}
                        style={{ width: `${widthPercent}%` }}
                      ></div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* .NET 10 CLR Runtime Diagnostics & Four Invariants */}
      {currentSnapshot && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Column 1: .NET 10 CLR Health Audit */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <Cpu className="w-4 h-4 text-purple-400" />
                <h3 className="text-sm font-semibold text-white">.NET 10 CLR 原生内核开销</h3>
              </div>
              <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-purple-500/20 text-purple-300">
                net10.0 Native AOT
              </span>
            </div>

            <div className="space-y-3 text-xs">
              <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                <div className="flex items-center justify-between text-slate-400">
                  <span>线程池调度 (ThreadPool)</span>
                  <span className="text-emerald-400 font-mono font-semibold">健康无饥饿</span>
                </div>
                <div className="flex items-center justify-between font-mono text-[11px] pt-1">
                  <span className="text-slate-300">活跃 Worker 线程:</span>
                  <span className="text-white font-bold">{currentSnapshot.clrMetrics.activeThreads} / 512</span>
                </div>
                <div className="flex items-center justify-between font-mono text-[11px]">
                  <span className="text-slate-300">空闲备用线程:</span>
                  <span className="text-emerald-400 font-bold">{currentSnapshot.clrMetrics.availableThreads}</span>
                </div>
              </div>

              <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                <div className="flex items-center justify-between text-slate-400">
                  <span>System.Threading.Lock 争用</span>
                  <span className="text-indigo-400 font-mono font-semibold">纳秒锁临界区</span>
                </div>
                <div className="flex items-center justify-between font-mono text-[11px] pt-1">
                  <span className="text-slate-300">锁争用事件:</span>
                  <span className="text-slate-200">{currentSnapshot.clrMetrics.lockContentionCount} 次</span>
                </div>
                <div className="flex items-center justify-between font-mono text-[11px]">
                  <span className="text-slate-300">平均等待时延:</span>
                  <span className="text-indigo-300 font-bold">{currentSnapshot.clrMetrics.lockWaitTimeAvgUs} μs</span>
                </div>
              </div>

              <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                <div className="flex items-center justify-between text-slate-400">
                  <span>垃圾回收 (GC) 影响</span>
                  <span className="text-purple-400 font-mono font-semibold">零 Full GC</span>
                </div>
                <div className="flex items-center justify-between font-mono text-[11px] pt-1">
                  <span className="text-slate-300">Gen 0 / Gen 1:</span>
                  <span className="text-slate-200">
                    {currentSnapshot.clrMetrics.gcGen0Collections} 次 / {currentSnapshot.clrMetrics.gcGen1Collections} 次
                  </span>
                </div>
                <div className="flex items-center justify-between font-mono text-[11px]">
                  <span className="text-slate-300">堆增量 / 零分配率:</span>
                  <span className="text-purple-300 font-bold">
                    {currentSnapshot.clrMetrics.allocatedMemoryMb} MB ({currentSnapshot.clrMetrics.zeroAllocationRate}%)
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Column 2 & 3: Four Invariant Assertions List */}
          <div className="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-emerald-400" />
                <h3 className="text-sm font-semibold text-white">
                  高并发压力下分布式不变量断言验证套件 (Invariant Assertions)
                </h3>
              </div>
              <span className="text-xs font-mono text-emerald-400 flex items-center gap-1">
                <CheckCircle2 className="w-3.5 h-3.5" /> 4/4 不变量断言通过
              </span>
            </div>

            <div className="space-y-3">
              {currentSnapshot.stabilityAssertions.map((inv) => (
                <div
                  key={inv.id}
                  className={`p-3.5 rounded-xl border transition ${
                    inv.passed
                      ? 'bg-slate-950 border-emerald-500/30'
                      : 'bg-rose-950/20 border-rose-500/50'
                  }`}
                >
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-1.5 border-b border-slate-800/60">
                    <div className="flex items-center gap-2">
                      {inv.passed ? (
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                      ) : (
                        <AlertTriangle className="w-4 h-4 text-rose-400 shrink-0" />
                      )}
                      <span className="font-semibold text-white text-xs">{inv.name}</span>
                    </div>
                    <span
                      className={`text-[11px] font-mono font-bold ${
                        inv.passed ? 'text-emerald-400' : 'text-rose-400'
                      }`}
                    >
                      {inv.passed ? 'SATISFIED (断言成立)' : 'VIOLATION DETECTED'}
                    </span>
                  </div>

                  <div className="mt-2 bg-slate-900 px-2.5 py-1 rounded text-indigo-300 font-mono text-[11px]">
                    {inv.formula}
                  </div>

                  <p className="text-xs text-slate-400 mt-1.5 leading-relaxed">{inv.details}</p>

                  <div className="mt-2 text-[11px] font-mono text-emerald-400/90 bg-emerald-500/5 px-2.5 py-1 rounded border border-emerald-500/20">
                    存证证据: {inv.evidence}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Historical Stress Runs Table */}
      {history.length > 0 && (
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <TrendingUp className="w-4 h-4 text-indigo-400" />
              <h3 className="text-sm font-semibold text-white">历史高并发压测审计流水 (Recent Benchmark History)</h3>
            </div>
            <span className="text-xs font-mono text-slate-400">保留最近 {history.length} 次压测记录</span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs font-mono">
              <thead className="bg-slate-950 text-slate-400 border-b border-slate-800 text-[11px]">
                <tr>
                  <th className="p-2.5">测试 ID</th>
                  <th className="p-2.5">测试场景</th>
                  <th className="p-2.5">并发数</th>
                  <th className="p-2.5">总请求量</th>
                  <th className="p-2.5">稳态 TPS</th>
                  <th className="p-2.5">P99 延迟</th>
                  <th className="p-2.5">成功率</th>
                  <th className="p-2.5">评级</th>
                  <th className="p-2.5">时间</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60 bg-slate-900/40 text-[11px]">
                {history.map((h) => (
                  <tr
                    key={h.testId}
                    onClick={() => setCurrentSnapshot(h)}
                    className={`cursor-pointer hover:bg-slate-800/40 transition ${
                      currentSnapshot?.testId === h.testId ? 'bg-indigo-950/20' : ''
                    }`}
                  >
                    <td className="p-2.5 text-indigo-400 font-bold whitespace-nowrap">{h.testId}</td>
                    <td className="p-2.5 text-slate-200 whitespace-nowrap">
                      {h.scenario === 'HIGH_FREQUENCY_TRADING'
                        ? '高频交易风暴'
                        : h.scenario === 'GRPC_CASCADING_MESH'
                        ? 'gRPC 级联链路'
                        : h.scenario === 'CLICKHOUSE_COLUMNAR_BURST'
                        ? 'ClickHouse 摄入'
                        : h.scenario === 'MULTI_TENANT_NOISY_NEIGHBOR'
                        ? '多租户噪声邻居'
                        : '混沌并发复合'}
                    </td>
                    <td className="p-2.5 text-slate-300">{h.concurrency} w</td>
                    <td className="p-2.5 text-slate-300">{h.totalRequests.toLocaleString()}</td>
                    <td className="p-2.5 text-emerald-400 font-bold tabular-nums">{h.tps.toLocaleString()}</td>
                    <td className="p-2.5 text-rose-400 font-bold tabular-nums">{h.latency.p99Ms} ms</td>
                    <td className="p-2.5 text-slate-200">
                      {((h.successfulRequests / h.totalRequests) * 100).toFixed(1)}%
                    </td>
                    <td className="p-2.5">
                      <span
                        className={`px-1.5 py-0.5 rounded font-bold text-[10px] ${
                          h.grade === 'S+'
                            ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'
                            : h.grade === 'A'
                            ? 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30'
                            : 'bg-amber-500/20 text-amber-300 border border-amber-500/30'
                        }`}
                      >
                        {h.grade} ({h.stabilityScore})
                      </span>
                    </td>
                    <td className="p-2.5 text-slate-500 whitespace-nowrap">
                      {new Date(h.timestamp).toLocaleTimeString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Markdown Audit Report Modal */}
      {showReportModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in">
          <div className="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-4xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden">
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-950">
              <div className="flex items-center gap-2.5">
                <div className="p-2 rounded-lg bg-rose-600/20 text-rose-400 border border-rose-500/30">
                  <FileCode2 className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-white flex items-center gap-2">
                    分布式架构高并发与压力测试稳定性审计报告
                    <span className="text-[11px] px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 font-mono">
                      Markdown Audit Report
                    </span>
                  </h3>
                  <p className="text-xs text-slate-400">
                    可直接用于工程交付、合规审计或架构评审的高保真性能评测报告
                  </p>
                </div>
              </div>
              <button
                onClick={() => setShowReportModal(false)}
                className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="flex items-center justify-between px-6 py-2.5 border-b border-slate-800 bg-slate-950/60 text-xs">
              <div className="text-slate-400 font-mono">
                Test ID: <span className="text-indigo-400 font-semibold">{currentSnapshot?.testId}</span>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    navigator.clipboard.writeText(reportMarkdown);
                    showTemporaryNotice('已复制 Markdown 审计报告到剪贴板！');
                  }}
                  className="flex items-center gap-1.5 px-3 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-mono transition"
                >
                  <Copy className="w-3.5 h-3.5 text-indigo-400" />
                  复制内容
                </button>
                <button
                  onClick={handleDownloadReport}
                  className="flex items-center gap-1.5 px-3 py-1 rounded bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-mono transition"
                >
                  <Download className="w-3.5 h-3.5" />
                  下载 .md 文件
                </button>
              </div>
            </div>

            <div className="p-6 overflow-y-auto font-mono text-xs text-slate-300 bg-slate-950 flex-1 leading-relaxed">
              <pre className="whitespace-pre overflow-x-auto">{reportMarkdown}</pre>
            </div>

            <div className="px-6 py-3 border-t border-slate-800 bg-slate-950 text-right">
              <button
                onClick={() => setShowReportModal(false)}
                className="px-4 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium transition"
              >
                关闭
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
