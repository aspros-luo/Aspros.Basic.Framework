import React, { useState, useEffect } from 'react';
import {
  Database,
  Server,
  Brain,
  HardDrive,
  CheckCircle2,
  AlertTriangle,
  RefreshCw,
  Zap,
  Layers,
  ArrowRight,
  ExternalLink,
  ShieldCheck,
  Check,
  Copy,
  Sliders,
  Sparkles,
} from 'lucide-react';

interface InfrastructureStatus {
  database: {
    engine: string;
    driver: string;
    filePath: string;
    fileSizeBytes: number;
    journalMode: string;
    totalRecords: number;
    tableCounts: {
      orders: number;
      inventories: number;
      payments: number;
      capMessages: number;
    };
    totalWritesCount: number;
    status: string;
  };
  clickhouse: {
    mode: 'REMOTE_CLUSTER' | 'LOCAL_PERSISTENT_COLUMNAR';
    remote: {
      host: string;
      port: number;
      database: string;
      username: string;
      connected: boolean;
      pingMs: number;
      lastChecked: string;
      error?: string;
    };
    local: {
      engine: string;
      filePath: string;
      fileSizeBytes: number;
      activeRows: number;
      activePartitions: number;
      compressionRatio: number;
    };
  };
  aiEngine: {
    geminiApiKeyConfigured: boolean;
    activeModel: string;
    vectorEngine: string;
    similarityMetric: string;
    totalCorpusChunks: number;
    vectorIndexPath: string;
    vectorIndexSizeBytes: number;
    keyNotice: string;
  };
}

export function InfrastructureConnectorDashboard() {
  const [status, setStatus] = useState<InfrastructureStatus | null>(null);
  const [loading, setLoading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  // ClickHouse remote config form
  const [ckHost, setCkHost] = useState('play.clickhouse.com');
  const [ckPort, setCkPort] = useState(443);
  const [ckUser, setCkUser] = useState('play');
  const [ckPassword, setCkPassword] = useState('');
  const [ckDatabase, setCkDatabase] = useState('default');
  const [ckConnecting, setCkConnecting] = useState(false);
  const [ckResultNotice, setCkResultNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    loadStatus();
  }, []);

  const showToast = (msg: string) => {
    setNotice(msg);
    setTimeout(() => setNotice(null), 3000);
  };

  const loadStatus = async () => {
    setLoading(true);
    try {
      const res = await fetch('/api/framework/infrastructure/status');
      const data = await res.json();
      if (data.success) {
        setStatus(data);
      }
    } catch (err: any) {
      showToast(`获取基础设施状态失败: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleConnectRemoteClickHouse = async (e: React.FormEvent) => {
    e.preventDefault();
    setCkConnecting(true);
    setCkResultNotice(null);

    const fullHost = ckHost.startsWith('http') ? ckHost : `${ckPort === 443 ? 'https' : 'http'}://${ckHost}:${ckPort}`;

    try {
      const res = await fetch('/api/framework/ck/configure-remote', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          host: fullHost,
          port: ckPort,
          username: ckUser,
          password: ckPassword,
          database: ckDatabase,
        }),
      });
      const data = await res.json();
      if (data.success) {
        setCkResultNotice({
          type: 'success',
          text: `成功连接远程 ClickHouse 集群！Ping 往返耗时: ${data.pingMs}ms。后续分析查询将直连该集群执行。`,
        });
        await loadStatus();
      } else {
        setCkResultNotice({
          type: 'error',
          text: `连接失败: ${data.error || '无法连通目标 ClickHouse 实例，已自动安全回退至本地持久化列存引擎。'}`,
        });
        await loadStatus();
      }
    } catch (err: any) {
      setCkResultNotice({
        type: 'error',
        text: `请求异常: ${err.message}`,
      });
    } finally {
      setCkConnecting(false);
    }
  };

  return (
    <div className="space-y-6 animate-in fade-in pb-12">
      {notice && (
        <div className="fixed top-5 right-5 z-50 bg-slate-900 border border-indigo-500/50 text-indigo-200 px-4 py-2.5 rounded-xl shadow-2xl text-xs font-mono flex items-center gap-2 animate-bounce">
          <Sparkles className="w-4 h-4 text-emerald-400" />
          <span>{notice}</span>
        </div>
      )}

      {/* Header Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950/40 to-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-blue-600/20 text-blue-400 border border-blue-500/30">
              <Server className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-xl font-bold text-white tracking-tight">
                  基础设施与外部持久化连接控制台
                </h2>
                <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-blue-500/20 text-blue-300 border border-blue-500/30 font-semibold">
                  Dual-Track Storage Active
                </span>
              </div>
              <p className="text-xs text-slate-400 mt-0.5">
                实时贯通【真实磁盘关系型数据库 (SQLite WAL)】、【ClickHouse 双轨列存 (本地文件 / 远程集群)】与【RAG 128维稠密语义向量引擎】。
              </p>
            </div>
          </div>

          <button
            onClick={loadStatus}
            disabled={loading}
            className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium transition border border-slate-700"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
            刷新数据源状态
          </button>
        </div>
      </div>

      {/* 3 Pillars Real Infrastructure Overview */}
      {status && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Pillar 1: Real RDBMS (SQLite 3.45 on Disk) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4 flex flex-col justify-between">
            <div className="space-y-3">
              <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                <div className="flex items-center gap-2">
                  <Database className="w-5 h-5 text-blue-400" />
                  <h3 className="text-sm font-semibold text-white">1. 真实关系型数据库 (RDBMS)</h3>
                </div>
                <span className="flex items-center gap-1 text-[11px] font-mono text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/30">
                  <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                  CONNECTED
                </span>
              </div>

              <div className="space-y-2 text-xs">
                <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1.5 font-mono">
                  <div className="flex justify-between text-slate-400">
                    <span>数据库引擎:</span>
                    <span className="text-white font-semibold">{status.database.engine}</span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>文件存储路径:</span>
                    <span className="text-blue-300 break-all select-all text-[11px]">
                      {status.database.filePath}
                    </span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>日志持久化模式:</span>
                    <span className="text-emerald-400 font-bold">{status.database.journalMode}</span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>磁盘文件大小:</span>
                    <span className="text-white">{(status.database.fileSizeBytes / 1024).toFixed(1)} KB</span>
                  </div>
                </div>

                {/* Table Breakdown */}
                <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1 text-[11px] font-mono">
                  <span className="text-slate-400 block font-sans font-medium text-xs mb-1">
                    真实表数据记录 (Total: {status.database.totalRecords} Rows)
                  </span>
                  <div className="grid grid-cols-2 gap-2 text-slate-300">
                    <div>orders (订单): <strong className="text-white">{status.database.tableCounts.orders}</strong></div>
                    <div>inventories (库存): <strong className="text-white">{status.database.tableCounts.inventories}</strong></div>
                    <div>payments (支付): <strong className="text-white">{status.database.tableCounts.payments}</strong></div>
                    <div>cap_messages (发件箱): <strong className="text-white">{status.database.tableCounts.capMessages}</strong></div>
                  </div>
                </div>
              </div>
            </div>

            <div className="pt-2 border-t border-slate-800/80 text-[11px] text-slate-400 leading-relaxed">
              💡 <strong>ACID 保证</strong>：每次在“工作单元与事务仿真”或高并发压测中发起的订单扣减，均真实写入该本地 SQLite 文件并通过事务原子锁定，杜绝内存伪造。
            </div>
          </div>

          {/* Pillar 2: ClickHouse OLAP (Local Columnar + Remote Cluster) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4 flex flex-col justify-between">
            <div className="space-y-3">
              <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                <div className="flex items-center gap-2">
                  <Layers className="w-5 h-5 text-amber-400" />
                  <h3 className="text-sm font-semibold text-white">2. ClickHouse 向量化数仓 (CK)</h3>
                </div>
                <span className={`text-[11px] font-mono px-2 py-0.5 rounded border ${
                  status.clickhouse.mode === 'REMOTE_CLUSTER'
                    ? 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30'
                    : 'bg-amber-500/10 text-amber-300 border-amber-500/30'
                }`}>
                  {status.clickhouse.mode === 'REMOTE_CLUSTER' ? 'REMOTE CLUSTER' : 'LOCAL PERSISTENT'}
                </span>
              </div>

              <div className="space-y-2 text-xs">
                <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1.5 font-mono">
                  <div className="flex justify-between text-slate-400">
                    <span>当前运行模式:</span>
                    <span className="text-amber-300 font-semibold">{status.clickhouse.mode}</span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>本地列存快照文件:</span>
                    <span className="text-slate-300 text-[11px] break-all">
                      {status.clickhouse.local.filePath}
                    </span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>活跃遥测日志行数:</span>
                    <span className="text-emerald-400 font-bold">{status.clickhouse.local.activeRows.toLocaleString()} Rows</span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>SIMD 压缩比:</span>
                    <span className="text-white">{status.clickhouse.local.compressionRatio}x (DoubleDelta)</span>
                  </div>
                </div>

                <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1 text-[11px] font-mono">
                  <div className="flex justify-between text-slate-400">
                    <span>远程集群连通状态:</span>
                    <span className={status.clickhouse.remote.connected ? 'text-emerald-400 font-bold' : 'text-slate-400'}>
                      {status.clickhouse.remote.connected ? `已连通 (${status.clickhouse.remote.pingMs}ms)` : '未连接 (运行于本地引擎)'}
                    </span>
                  </div>
                  {status.clickhouse.remote.error && (
                    <div className="text-rose-400 text-[10px] pt-1">
                      提示: {status.clickhouse.remote.error}
                    </div>
                  )}
                </div>
              </div>
            </div>

            <div className="pt-2 border-t border-slate-800/80 text-[11px] text-slate-400 leading-relaxed">
              💡 <strong>双轨驱动</strong>：支持通过下方表单随时填入公司 ClickHouse 或 ClickHouse Cloud 实例，由官方 <code>@clickhouse/client</code> 驱动实时集群 SQL。
            </div>
          </div>

          {/* Pillar 3: RAG & LLM Engine */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4 flex flex-col justify-between">
            <div className="space-y-3">
              <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                <div className="flex items-center gap-2">
                  <Brain className="w-5 h-5 text-purple-400" />
                  <h3 className="text-sm font-semibold text-white">3. RAG 稠密语义向量与 LLM</h3>
                </div>
                <span className={`text-[11px] font-mono px-2 py-0.5 rounded border ${
                  status.aiEngine.geminiApiKeyConfigured
                    ? 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30'
                    : 'bg-indigo-500/10 text-indigo-300 border-indigo-500/30'
                }`}>
                  {status.aiEngine.geminiApiKeyConfigured ? 'GEMINI LIVE' : 'DENSE VECTORS ACTIVE'}
                </span>
              </div>

              <div className="space-y-2 text-xs">
                <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1.5 font-mono">
                  <div className="flex justify-between text-slate-400">
                    <span>向量嵌入维度:</span>
                    <span className="text-purple-300 font-bold">128-Dim Dense Vectors</span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>相似度数学度量:</span>
                    <span className="text-emerald-400">{status.aiEngine.similarityMetric}</span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>向量索引落盘文件:</span>
                    <span className="text-slate-300 text-[11px] break-all">
                      {status.aiEngine.vectorIndexPath}
                    </span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>知识切片数:</span>
                    <span className="text-white">{status.aiEngine.totalCorpusChunks} 篇</span>
                  </div>
                </div>

                <div className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1 text-[11px]">
                  <div className="flex items-center justify-between text-slate-400 font-mono">
                    <span>Gemini API Key:</span>
                    <span className={status.aiEngine.geminiApiKeyConfigured ? 'text-emerald-400 font-bold' : 'text-amber-400'}>
                      {status.aiEngine.geminiApiKeyConfigured ? '已配置 (Live Mode)' : '未挂载 (Fallback Mode)'}
                    </span>
                  </div>
                  <p className="text-[10px] text-slate-400 pt-1 leading-relaxed">
                    {status.aiEngine.keyNotice}
                  </p>
                </div>
              </div>
            </div>

            <div className="pt-2 border-t border-slate-800/80 text-[11px] text-slate-400 leading-relaxed">
              💡 <strong>真向量语义检索</strong>：全部 6 篇架构切片与用户提问均由 128 维空间向量点积余弦计算相似度，并完整固化在 <code>data/rag_dense_embeddings.json</code>。
            </div>
          </div>
        </div>
      )}

      {/* Remote ClickHouse Cluster Connection Setup Form */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <div className="flex items-center justify-between border-b border-slate-800 pb-3">
          <div className="flex items-center gap-2">
            <Sliders className="w-5 h-5 text-amber-400" />
            <div>
              <h3 className="text-sm font-semibold text-white">
                远程 ClickHouse 集群实时直连配置 (Remote ClickHouse Live Connector)
              </h3>
              <p className="text-xs text-slate-400">
                支持连接外部任何自建 ClickHouse 服务或云端 ClickHouse Cloud（内置 @clickhouse/client 进行真实网络 Ping 与即席 SQL 转发）
              </p>
            </div>
          </div>
          <span className="text-xs font-mono text-slate-400">Port 8123 / 443 (HTTP/HTTPS)</span>
        </div>

        {ckResultNotice && (
          <div
            className={`p-3.5 rounded-xl text-xs font-mono border ${
              ckResultNotice.type === 'success'
                ? 'bg-emerald-950/20 border-emerald-500/40 text-emerald-300'
                : 'bg-rose-950/20 border-rose-500/40 text-rose-300'
            }`}
          >
            {ckResultNotice.text}
          </div>
        )}

        <form onSubmit={handleConnectRemoteClickHouse} className="space-y-4 text-xs font-mono">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
            <div>
              <label className="text-slate-400 block mb-1">ClickHouse Host</label>
              <input
                type="text"
                value={ckHost}
                onChange={(e) => setCkHost(e.target.value)}
                placeholder="例如: play.clickhouse.com"
                className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white"
              />
            </div>
            <div>
              <label className="text-slate-400 block mb-1">HTTP 端口 (Port)</label>
              <input
                type="number"
                value={ckPort}
                onChange={(e) => setCkPort(Number(e.target.value))}
                placeholder="8123 或 443"
                className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white"
              />
            </div>
            <div>
              <label className="text-slate-400 block mb-1">用户名 (User)</label>
              <input
                type="text"
                value={ckUser}
                onChange={(e) => setCkUser(e.target.value)}
                placeholder="default / play"
                className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white"
              />
            </div>
            <div>
              <label className="text-slate-400 block mb-1">密码 (Password)</label>
              <input
                type="password"
                value={ckPassword}
                onChange={(e) => setCkPassword(e.target.value)}
                placeholder="留空或输入集群密码"
                className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white"
              />
            </div>
            <div>
              <label className="text-slate-400 block mb-1">数据库名 (Database)</label>
              <input
                type="text"
                value={ckDatabase}
                onChange={(e) => setCkDatabase(e.target.value)}
                placeholder="default"
                className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white"
              />
            </div>
          </div>

          <div className="flex items-center justify-between pt-2">
            <span className="text-[11px] text-slate-500 font-sans">
              默认可填入 ClickHouse 官方公共游乐场 <code>play.clickhouse.com:443 (User: play, Password: 空)</code> 进行快速连通测试。
            </span>
            <button
              type="submit"
              disabled={ckConnecting}
              className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-amber-600 hover:bg-amber-500 text-white font-medium text-xs transition shadow-md shadow-amber-600/20 disabled:opacity-50"
            >
              {ckConnecting ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  正在连接集群并执行 Ping...
                </>
              ) : (
                <>
                  <Zap className="w-3.5 h-3.5" />
                  测试并连接远程 ClickHouse
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
