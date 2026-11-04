import React, { useState, useEffect } from 'react';
import {
  Brain,
  Sparkles,
  Bot,
  Database,
  Search,
  Zap,
  CheckCircle2,
  AlertTriangle,
  Play,
  RotateCcw,
  Copy,
  Layers,
  ArrowRight,
  Code2,
  Terminal,
  Clock,
  ShieldCheck,
  Send,
  Plus,
  RefreshCw,
  Cpu,
  Sliders,
  ChevronDown,
  ChevronUp,
  FileText,
  Activity,
  Check,
} from 'lucide-react';
import type {
  RagKnowledgeChunk,
  RagRetrievalResult,
  AgentWorkflowExecution,
  AiDiagnosticRequest,
  AiDiagnosticResponse,
} from '../types';

const PRESET_RAG_QUERIES = [
  '高并发扣减库存如何利用 .NET 10 System.Threading.Lock 避免死锁和超卖？',
  'CAP Outbox 发件箱模式如何保障业务实体与跨微服务事件的原子性？',
  'ClickHouse SIMD Vector512 向量化列存与物化视图是如何实现毫秒级下钻的？',
  '同机房就近亲和性路由 (Zone-Affinity) 如何解决跨可用区 22ms 网络开销？',
  'Jepsen 混沌并发注入与默克尔树机器证明如何验证分布式事务正确性？',
];

const PRESET_RCA_INPUTS = [
  `System.TimeoutException: gRPC call to 'xr.trade' timed out after 500ms. 
Deadline exceeded. Context cancelled by caller at Aspros.Gateway.ForwardAuthorizationHeader().
Current Zone: zone-east-1a, Downstream Node: 10.0.1.21 (zone-east-1b).
Active Worker Threads: 48, Lock Contention Count: 18.`,
  `Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: Database operation expected to affect 1 row(s) but affected 0 row(s).
Entity: InventoryEntity (SKU: SKU-PRO-SERVER-16C). Optimistic lock version mismatch under 300 concurrent workers.`,
];

const PRESET_NL_SQL_INPUTS = [
  '查询过去24小时内华东租户各个微服务模块的调用量、平均耗时与 P95 延迟',
  '统计哪些画像角色在高峰期产生了最高的微成本消耗，按降序排列',
  '查找延迟大于 10ms 的慢长尾请求及其对应的租户和事件类型',
];

export function AiIntelligenceDashboard() {
  const [activeAiTab, setActiveAiTab] = useState<'rag' | 'agent' | 'diagnostic'>('rag');

  // RAG State
  const [ragQuery, setRagQuery] = useState(PRESET_RAG_QUERIES[0]);
  const [ragLoading, setRagLoading] = useState(false);
  const [ragResult, setRagResult] = useState<RagRetrievalResult | null>(null);
  const [corpus, setCorpus] = useState<RagKnowledgeChunk[]>([]);
  const [showPromptInspector, setShowPromptInspector] = useState(false);
  const [showAddChunkModal, setShowAddChunkModal] = useState(false);

  // New Chunk Form
  const [newChunkTitle, setNewChunkTitle] = useState('');
  const [newChunkCategory, setNewChunkCategory] = useState<'Architecture' | 'Runtime' | 'Distributed' | 'FaultTolerance' | 'Database'>('Architecture');
  const [newChunkContent, setNewChunkContent] = useState('');
  const [newChunkTags, setNewChunkTags] = useState('Microservice, .NET 10');

  // Agent State
  const [selectedAgentWorkflow, setSelectedAgentWorkflow] = useState<'INCIDENT_TRIAGE' | 'NOISY_NEIGHBOR' | 'FORMAL_AUDIT'>('INCIDENT_TRIAGE');
  const [agentRunning, setAgentRunning] = useState(false);
  const [agentExecution, setAgentExecution] = useState<AgentWorkflowExecution | null>(null);
  const [agentHistory, setAgentHistory] = useState<AgentWorkflowExecution[]>([]);

  // Diagnostics State
  const [diagnosticTask, setDiagnosticTask] = useState<'RCA_INCIDENT' | 'NATURAL_TO_SQL' | 'ARCHITECTURE_REVIEW'>('RCA_INCIDENT');
  const [diagnosticInput, setDiagnosticInput] = useState(PRESET_RCA_INPUTS[0]);
  const [diagnosticUseRag, setDiagnosticUseRag] = useState(true);
  const [diagnosticLoading, setDiagnosticLoading] = useState(false);
  const [diagnosticResult, setDiagnosticResult] = useState<AiDiagnosticResponse | null>(null);

  // AI Status
  const [aiStatus, setAiStatus] = useState<any>(null);

  // Notice
  const [notice, setNotice] = useState<string | null>(null);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  useEffect(() => {
    loadCorpus();
    loadAgentHistory();
    loadAiStatus();
  }, []);

  const loadAiStatus = async () => {
    try {
      const res = await fetch('/api/framework/ai/status');
      const data = await res.json();
      if (data.success && data.status) {
        setAiStatus(data.status);
      }
    } catch {}
  };

  const showToast = (msg: string) => {
    setNotice(msg);
    setTimeout(() => setNotice(null), 3000);
  };

  const handleCopy = (text: string, key: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(key);
    showToast('已复制内容到剪贴板');
    setTimeout(() => setCopiedKey(null), 2000);
  };

  const loadCorpus = async () => {
    try {
      const res = await fetch('/api/framework/ai/rag/corpus');
      const data = await res.json();
      if (data.success && data.corpus) {
        setCorpus(data.corpus);
      }
    } catch {
      // Graceful
    }
  };

  const loadAgentHistory = async () => {
    try {
      const res = await fetch('/api/framework/ai/agent/history');
      const data = await res.json();
      if (data.success && data.history?.length) {
        setAgentHistory(data.history);
        setAgentExecution(data.history[0]);
      }
    } catch {
      // Graceful
    }
  };

  // Run RAG Query
  const handleExecuteRag = async () => {
    if (!ragQuery.trim()) return;
    setRagLoading(true);
    setRagResult(null);
    try {
      const res = await fetch('/api/framework/ai/rag/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query: ragQuery }),
      });
      const data = await res.json();
      if (data.success && data.result) {
        setRagResult(data.result);
        showToast('RAG 知识检索与推理完成！');
      } else {
        showToast(`RAG 推理异常: ${data.error || '未知错误'}`);
      }
    } catch (err: any) {
      showToast(`请求失败: ${err.message}`);
    } finally {
      setRagLoading(false);
    }
  };

  // Run Autonomous Agent
  const handleRunAgent = async () => {
    if (agentRunning) return;
    setAgentRunning(true);
    try {
      const res = await fetch('/api/framework/ai/agent/run', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ workflowType: selectedAgentWorkflow }),
      });
      const data = await res.json();
      if (data.success && data.execution) {
        setAgentExecution(data.execution);
        setAgentHistory((prev) => [data.execution, ...prev.slice(0, 9)]);
        showToast('AI Agent 自主编排工作流执行完毕！');
      } else {
        showToast(`Agent 执行失败: ${data.error || '未知错误'}`);
      }
    } catch (err: any) {
      showToast(`Agent 请求异常: ${err.message}`);
    } finally {
      setAgentRunning(false);
    }
  };

  // Run Diagnostic
  const handleRunDiagnostic = async () => {
    if (!diagnosticInput.trim()) return;
    setDiagnosticLoading(true);
    setDiagnosticResult(null);
    try {
      const req: AiDiagnosticRequest = {
        taskType: diagnosticTask,
        inputContext: diagnosticInput,
        useRag: diagnosticUseRag,
      };
      const res = await fetch('/api/framework/ai/diagnostic', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      });
      const data = await res.json();
      if (data.success) {
        setDiagnosticResult(data);
        showToast('AI 智能诊断完成！');
      } else {
        showToast(`诊断异常: ${data.error || '未知错误'}`);
      }
    } catch (err: any) {
      showToast(`请求错误: ${err.message}`);
    } finally {
      setDiagnosticLoading(false);
    }
  };

  // Add Chunk to Knowledge Base
  const handleAddChunk = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newChunkTitle || !newChunkContent) return;
    try {
      const res = await fetch('/api/framework/ai/rag/chunk', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title: newChunkTitle,
          category: newChunkCategory,
          content: newChunkContent,
          tags: newChunkTags.split(',').map((t) => t.trim()),
        }),
      });
      const data = await res.json();
      if (data.success && data.corpus) {
        setCorpus(data.corpus);
        setShowAddChunkModal(false);
        setNewChunkTitle('');
        setNewChunkContent('');
        showToast('已成功添加新知识切片到向量库！');
      }
    } catch (err: any) {
      showToast(`添加切片失败: ${err.message}`);
    }
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
        <div className="absolute top-0 right-0 w-96 h-96 bg-indigo-500/5 rounded-full blur-3xl pointer-events-none"></div>
        <div className="relative z-10 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
          <div className="space-y-2">
            <div className="flex items-center gap-2.5">
              <div className="p-2.5 rounded-xl bg-indigo-600/20 text-indigo-400 border border-indigo-500/30">
                <Brain className="w-6 h-6" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-xl font-bold text-white tracking-tight">
                    微服务 AI 智能体与 RAG 知识检索工作台
                  </h2>
                  <span className={`px-2 py-0.5 rounded text-[11px] font-mono font-semibold ${
                    aiStatus?.geminiApiKeyConfigured
                      ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'
                      : 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30'
                  }`}>
                    {aiStatus?.geminiApiKeyConfigured ? 'gemini-3.8-flash (Live Cloud)' : '128-Dim Dense Vector RAG'}
                  </span>
                  <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-purple-500/20 text-purple-300 border border-purple-500/30 font-semibold">
                    Autonomous ReAct Agent
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">
                  深度融合大语言模型 (LLM)、向量化检索增强 (RAG) 与自主多步工具调用闭环智能体，为 Aspros 分布式微服务提供全链路自主自治能力。
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 text-xs bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700">
              <span className={`w-2 h-2 rounded-full ${aiStatus?.geminiApiKeyConfigured ? 'bg-emerald-400 animate-pulse' : 'bg-indigo-400'}`}></span>
              <span className="text-slate-300 font-mono">
                {aiStatus?.geminiApiKeyConfigured ? 'Cloud Gemini: Online' : 'Vector RAG: Active'}
              </span>
            </div>
            <button
              onClick={() => setShowAddChunkModal(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium transition shadow-sm"
            >
              <Plus className="w-3.5 h-3.5" />
              添加知识切片
            </button>
          </div>
        </div>

        {aiStatus && !aiStatus.geminiApiKeyConfigured && (
          <div className="mt-4 p-3 rounded-xl bg-slate-950/80 border border-indigo-900/50 flex items-start gap-2.5 text-xs text-slate-300">
            <Sparkles className="w-4 h-4 text-indigo-400 mt-0.5 shrink-0" />
            <div>
              <span className="font-semibold text-indigo-300">本地 128 维稠密向量 RAG 引擎就绪：</span>
              <span className="text-slate-400 ml-1">{aiStatus.keyNotice}</span>
            </div>
          </div>
        )}

        {/* 3 Pillars Tab Switcher */}
        <div className="mt-6 flex flex-wrap gap-2 border-t border-slate-800/80 pt-4">
          <button
            onClick={() => setActiveAiTab('rag')}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition ${
              activeAiTab === 'rag'
                ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-600/20'
                : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'
            }`}
          >
            <Database className="w-4 h-4" />
            1. RAG 知识检索增强大模型 (Retrieval-Augmented Generation)
          </button>
          <button
            onClick={() => setActiveAiTab('agent')}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition ${
              activeAiTab === 'agent'
                ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-600/20'
                : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'
            }`}
          >
            <Bot className="w-4 h-4" />
            2. AI Agent 自主多步闭环工作流 (Autonomous ReAct & Tool Use)
          </button>
          <button
            onClick={() => setActiveAiTab('diagnostic')}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition ${
              activeAiTab === 'diagnostic'
                ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-600/20'
                : 'bg-slate-950 text-slate-400 hover:text-slate-200 border border-slate-800'
            }`}
          >
            <Zap className="w-4 h-4" />
            3. 故障根因分析与 NL to SQL (Copilot & Diagnostics)
          </button>
        </div>
      </div>

      {/* PILLAR 1: RAG WORKBENCH */}
      {activeAiTab === 'rag' && (
        <div className="space-y-6">
          {/* Query input card */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <Search className="w-4 h-4 text-indigo-400" />
                <h3 className="text-sm font-semibold text-white">
                  架构知识库向量检索与精准增强问答 (Vector Search & LLM Synthesis)
                </h3>
              </div>
              <span className="text-[11px] font-mono text-slate-400">
                已索引 <strong className="text-indigo-400">{corpus.length}</strong> 篇微服务专业架构切片
              </span>
            </div>

            {/* Presets */}
            <div className="space-y-1.5">
              <span className="text-xs text-slate-400 block">预设微服务深度架构问答（点击填充）：</span>
              <div className="flex flex-wrap gap-2">
                {PRESET_RAG_QUERIES.map((q, idx) => (
                  <button
                    key={idx}
                    onClick={() => setRagQuery(q)}
                    className="text-left text-[11px] px-2.5 py-1 rounded-lg bg-slate-950 hover:bg-slate-800 text-slate-300 border border-slate-800 hover:border-indigo-500/40 transition"
                  >
                    {q}
                  </button>
                ))}
              </div>
            </div>

            {/* Input Bar */}
            <div className="flex items-center gap-2">
              <input
                type="text"
                value={ragQuery}
                onChange={(e) => setRagQuery(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleExecuteRag()}
                placeholder="输入您想向 Aspros 知识库提问的微服务架构问题..."
                className="flex-1 bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 font-sans"
              />
              <button
                onClick={handleExecuteRag}
                disabled={ragLoading || !ragQuery.trim()}
                className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-xs transition shadow-md shadow-indigo-600/20 disabled:opacity-50"
              >
                {ragLoading ? (
                  <>
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                    检索与生成中...
                  </>
                ) : (
                  <>
                    <Sparkles className="w-3.5 h-3.5" />
                    RAG 检索增强
                  </>
                )}
              </button>
            </div>
          </div>

          {/* RAG Retrieval Results & LLM Answer */}
          {ragResult && (
            <div className="space-y-6">
              {/* Top Retrieved Chunks Cards */}
              <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
                <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                  <div className="flex items-center gap-2">
                    <Database className="w-4 h-4 text-emerald-400" />
                    <h3 className="text-sm font-semibold text-white">
                      透明化检索结果：匹配到的 Top-{ragResult.topChunks.length} 核心知识切片 (Retrieved Chunks)
                    </h3>
                  </div>
                  <div className="flex items-center gap-3 text-xs font-mono text-slate-400">
                    <span>向量召回耗时: <strong className="text-emerald-400">{ragResult.retrievalTimeMs}ms</strong></span>
                    <span>生成耗时: <strong className="text-indigo-400">{ragResult.generationTimeMs}ms</strong></span>
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                  {ragResult.topChunks.map((chunk, idx) => (
                    <div
                      key={chunk.id}
                      className="p-3.5 rounded-xl bg-slate-950 border border-slate-800 hover:border-indigo-500/40 transition flex flex-col justify-between"
                    >
                      <div>
                        <div className="flex items-center justify-between text-xs pb-1 border-b border-slate-850">
                          <span className="font-mono text-indigo-400 font-semibold">{chunk.id}</span>
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-emerald-500/10 text-emerald-400 font-mono font-bold">
                            {(chunk.relevanceScore! * 100).toFixed(0)}% Match
                          </span>
                        </div>
                        <h4 className="text-xs font-semibold text-white mt-2 leading-snug">{chunk.title}</h4>
                        <p className="text-[11px] text-slate-400 mt-1.5 line-clamp-4 leading-relaxed">
                          {chunk.content}
                        </p>
                      </div>
                      <div className="mt-3 pt-2 border-t border-slate-900 flex flex-wrap gap-1">
                        {chunk.tags.map((t, tIdx) => (
                          <span key={tIdx} className="text-[9px] font-mono px-1 py-0.5 rounded bg-slate-900 text-slate-400">
                            #{t}
                          </span>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>

                {/* Augmented Prompt Toggle */}
                <div className="pt-2">
                  <button
                    onClick={() => setShowPromptInspector(!showPromptInspector)}
                    className="flex items-center gap-1.5 text-xs text-indigo-400 hover:text-indigo-300 font-mono"
                  >
                    {showPromptInspector ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
                    {showPromptInspector ? '收起拼接后的增强 Prompt (Augmented Prompt)' : '展开查看拼接注入上下文后的增强 Prompt'}
                  </button>
                  {showPromptInspector && (
                    <div className="mt-2.5 p-3.5 rounded-xl bg-slate-950 border border-slate-850 font-mono text-[11px] text-slate-300 whitespace-pre-wrap leading-relaxed max-h-60 overflow-y-auto">
                      {ragResult.augmentedPrompt}
                    </div>
                  )}
                </div>
              </div>

              {/* LLM Synthesis Output Card */}
              <div className="bg-slate-900 border border-indigo-500/30 rounded-2xl p-6 shadow-xl space-y-4">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
                  <div className="flex items-center gap-2">
                    <Sparkles className="w-5 h-5 text-indigo-400" />
                    <div>
                      <h3 className="text-sm font-semibold text-white">
                        大语言模型推理与精准答复 (LLM Synthesized Answer)
                      </h3>
                      <p className="text-[11px] text-slate-400">
                        基于 Aspros 知识切片严谨生成，无幻觉、严格契合 .NET 10 / C# 14 现代规范
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-[11px] font-mono px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                      Model: {ragResult.sourceModel}
                    </span>
                    <button
                      onClick={() => handleCopy(ragResult.llmAnswer, 'rag_answer')}
                      className="flex items-center gap-1 px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-mono transition"
                    >
                      {copiedKey === 'rag_answer' ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3 text-indigo-400" />}
                      复制解答
                    </button>
                  </div>
                </div>

                <div className="prose prose-invert prose-xs max-w-none text-slate-200 text-xs leading-relaxed space-y-3 font-sans">
                  <div className="whitespace-pre-wrap leading-relaxed">
                    {ragResult.llmAnswer}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Knowledge Corpus Overview */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <Layers className="w-4 h-4 text-purple-400" />
                <h3 className="text-sm font-semibold text-white">Aspros 向量知识切片库全景 (Corpus Inventory)</h3>
              </div>
              <button
                onClick={() => setShowAddChunkModal(true)}
                className="text-xs text-indigo-400 hover:text-indigo-300 font-mono flex items-center gap-1"
              >
                <Plus className="w-3.5 h-3.5" /> 新建切片
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              {corpus.map((item) => (
                <div key={item.id} className="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                  <div className="flex items-center justify-between text-xs">
                    <span className="font-mono text-purple-400 font-semibold">{item.id}</span>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-900 text-slate-400 font-mono">
                      {item.category}
                    </span>
                  </div>
                  <h4 className="text-xs font-medium text-white">{item.title}</h4>
                  <p className="text-[11px] text-slate-400 line-clamp-3 leading-relaxed">{item.content}</p>
                  <div className="pt-1 flex flex-wrap gap-1">
                    {item.tags.map((t, idx) => (
                      <span key={idx} className="text-[9px] font-mono text-slate-500">
                        #{t}
                      </span>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* PILLAR 2: AUTONOMOUS AGENT WORKFLOWS */}
      {activeAiTab === 'agent' && (
        <div className="space-y-6">
          {/* Agent Selection & Run Bar */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <Bot className="w-5 h-5 text-indigo-400" />
                <div>
                  <h3 className="text-sm font-semibold text-white">
                    微服务自主多步闭环智能体 (Autonomous ReAct & Tool Use Engine)
                  </h3>
                  <p className="text-xs text-slate-400">
                    具备自主规划 (Plan) &rarr; 工具调用 (Act) &rarr; 观察反馈 (Observe) &rarr; 自省校准 (Reflect) 的生产级闭环智能体
                  </p>
                </div>
              </div>
              <button
                onClick={handleRunAgent}
                disabled={agentRunning}
                className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-500 text-white font-semibold text-xs transition shadow-lg shadow-rose-600/20 disabled:opacity-50"
              >
                {agentRunning ? (
                  <>
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                    Agent 正在多步规划执行中...
                  </>
                ) : (
                  <>
                    <Play className="w-3.5 h-3.5 fill-current" />
                    触发自主 Agent 闭环执行
                  </>
                )}
              </button>
            </div>

            {/* Workflow Presets */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              {[
                {
                  id: 'INCIDENT_TRIAGE',
                  title: '生产链路故障自愈与熔断巡检 Agent',
                  desc: '自主探查 ClickHouse P95 延迟与跨区长尾调用，巡检微服务熔断器健康，并制定就近亲和性自愈策略。',
                  tools: ['Tool_QueryClickHouse', 'Tool_InspectCircuitBreaker'],
                },
                {
                  id: 'NOISY_NEIGHBOR',
                  title: '多租户噪声邻居与资源均衡 Agent',
                  desc: '审计租户 100 与 200 计算资源抢占情况，验证 AsyncLocal<WorkContext> 异步租户隔离防线。',
                  tools: ['Tool_QueryClickHouse', 'Tool_VerifyIdempotencyState'],
                },
                {
                  id: 'FORMAL_AUDIT',
                  title: '零超卖与幂等状态机形式化审计 Agent',
                  desc: '自动化全量对齐库存守恒数学公式，审查并发幂等账本，确立 SHA-256 默克尔根机器证明合规性。',
                  tools: ['Tool_VerifyIdempotencyState', 'Tool_GenerateDotNet10Scaffold'],
                },
              ].map((wf) => {
                const isSelected = selectedAgentWorkflow === wf.id;
                return (
                  <button
                    key={wf.id}
                    onClick={() => setSelectedAgentWorkflow(wf.id as any)}
                    className={`p-3.5 rounded-xl border text-left transition flex flex-col justify-between ${
                      isSelected
                        ? 'bg-indigo-950/30 border-indigo-500/60 shadow-md ring-1 ring-indigo-500/30'
                        : 'bg-slate-950 border-slate-800 hover:border-slate-700'
                    }`}
                  >
                    <div>
                      <div className="flex items-center justify-between text-xs">
                        <span className="font-semibold text-white">{wf.title}</span>
                        {isSelected && <span className="w-2 h-2 rounded-full bg-emerald-400"></span>}
                      </div>
                      <p className="text-[11px] text-slate-400 mt-1.5 leading-relaxed">{wf.desc}</p>
                    </div>
                    <div className="mt-3 pt-2 border-t border-slate-800/80 flex flex-wrap gap-1">
                      {wf.tools.map((t, idx) => (
                        <span key={idx} className="text-[9px] font-mono px-1 py-0.5 rounded bg-slate-900 text-indigo-300">
                          {t}
                        </span>
                      ))}
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Active Agent Execution Trace */}
          {agentExecution && (
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-800">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-xs text-indigo-400 font-bold">{agentExecution.workflowId}</span>
                    <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/20 text-emerald-300 font-bold">
                      {agentExecution.status}
                    </span>
                    <span className="text-xs font-mono text-slate-400">耗时: {agentExecution.totalDurationMs}ms</span>
                  </div>
                  <h3 className="text-base font-bold text-white mt-1">{agentExecution.workflowName}</h3>
                  <p className="text-xs text-slate-400 mt-0.5">目标: {agentExecution.goal}</p>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-[11px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-300">
                    Model: {agentExecution.modelUsed}
                  </span>
                  <button
                    onClick={() => handleCopy(agentExecution.finalSynthesis, 'agent_synthesis')}
                    className="flex items-center gap-1 px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-mono transition"
                  >
                    {copiedKey === 'agent_synthesis' ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3 text-indigo-400" />}
                    复制决策结论
                  </button>
                </div>
              </div>

              {/* Step-by-Step Decision Timeline (ReAct Loop) */}
              <div className="space-y-4">
                <span className="text-xs font-mono text-slate-400 block font-semibold">
                  智能体自主思考与工具调用链 (Autonomous Thought & Tool Action Chain)
                </span>

                <div className="space-y-3 relative before:absolute before:inset-0 before:left-3.5 before:w-0.5 before:bg-slate-800">
                  {agentExecution.steps.map((step) => {
                    const isThink = step.phase === 'THINK';
                    const isAct = step.phase === 'ACT';
                    const isObserve = step.phase === 'OBSERVE';
                    const isReflect = step.phase === 'REFLECT';

                    return (
                      <div key={step.stepNumber} className="relative flex items-start gap-3 pl-1">
                        <div
                          className={`w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-mono font-bold shrink-0 z-10 ${
                            isThink
                              ? 'bg-indigo-600 text-white'
                              : isAct
                              ? 'bg-rose-600 text-white'
                              : isObserve
                              ? 'bg-blue-600 text-white'
                              : 'bg-emerald-600 text-white'
                          }`}
                        >
                          {step.stepNumber}
                        </div>

                        <div className="flex-1 bg-slate-950 border border-slate-800 rounded-xl p-3.5 space-y-2">
                          <div className="flex items-center justify-between text-xs">
                            <span
                              className={`font-mono text-[11px] font-bold ${
                                isThink
                                  ? 'text-indigo-400'
                                  : isAct
                                  ? 'text-rose-400'
                                  : isObserve
                                  ? 'text-blue-400'
                                  : 'text-emerald-400'
                              }`}
                            >
                              [{step.phase}] {isThink ? '思考规划' : isAct ? '执行系统工具' : isObserve ? '观测评估' : '自省沉淀'}
                            </span>
                            <span className="text-[10px] font-mono text-slate-500">
                              {new Date(step.timestamp).toLocaleTimeString()}
                            </span>
                          </div>

                          <p className="text-xs text-slate-300 leading-relaxed font-sans">{step.thought}</p>

                          {/* Tool Call Box */}
                          {step.toolCall && (
                            <div className="mt-2 bg-slate-900 border border-slate-800 rounded-lg p-3 font-mono text-[11px] space-y-1.5">
                              <div className="flex items-center justify-between text-rose-300 font-bold">
                                <span>🛠️ 调用系统工具: {step.toolCall.toolName}</span>
                                <span className="text-[10px] text-slate-400 font-normal">
                                  耗时: {step.toolCall.executionTimeMs}ms
                                </span>
                              </div>
                              <div className="text-slate-400">
                                入参: <code className="text-indigo-300">{JSON.stringify(step.toolCall.args)}</code>
                              </div>
                              <div className="text-emerald-400 pt-1 border-t border-slate-800/80">
                                返回结果: <span className="text-slate-200">{JSON.stringify(step.toolCall.result)}</span>
                              </div>
                            </div>
                          )}

                          {/* Reflection Box */}
                          {step.reflection && (
                            <div className="mt-2 bg-emerald-500/10 border border-emerald-500/30 rounded-lg p-2.5 text-xs text-emerald-300 font-sans">
                              <strong>自省校准结论:</strong> {step.reflection}
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Final Synthesis Report */}
              <div className="p-4 rounded-xl bg-slate-950 border border-indigo-500/40 space-y-2">
                <div className="flex items-center gap-2 text-indigo-400 text-xs font-bold font-mono">
                  <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                  Agent 最终综合自愈与治理报告 (Final Synthesized Decision)
                </div>
                <div className="text-xs text-slate-200 whitespace-pre-wrap leading-relaxed font-mono">
                  {agentExecution.finalSynthesis}
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* PILLAR 3: COPILOT & DIAGNOSTICS */}
      {activeAiTab === 'diagnostic' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <Zap className="w-5 h-5 text-indigo-400" />
                <div>
                  <h3 className="text-sm font-semibold text-white">
                    微服务 AI 诊断与智能开发助手 (Diagnostics & NL-to-SQL Copilot)
                  </h3>
                  <p className="text-xs text-slate-400">
                    针对分布式生产报错根因、ClickHouse 向量化查询与 .NET 10 架构合规提供一键智能分析
                  </p>
                </div>
              </div>
              <label className="flex items-center gap-2 text-xs font-mono text-indigo-300 cursor-pointer">
                <input
                  type="checkbox"
                  checked={diagnosticUseRag}
                  onChange={(e) => setDiagnosticUseRag(e.target.checked)}
                  className="rounded accent-indigo-500"
                />
                启用 RAG 知识检索注入
              </label>
            </div>

            {/* Task Type Switch */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-2">
              <button
                onClick={() => {
                  setDiagnosticTask('RCA_INCIDENT');
                  setDiagnosticInput(PRESET_RCA_INPUTS[0]);
                }}
                className={`p-3 rounded-xl border text-left transition ${
                  diagnosticTask === 'RCA_INCIDENT'
                    ? 'bg-rose-950/20 border-rose-500/60 shadow-sm'
                    : 'bg-slate-950 border-slate-800 hover:border-slate-700'
                }`}
              >
                <div className="text-xs font-bold text-white">分布式故障根因诊断 (RCA)</div>
                <div className="text-[11px] text-slate-400 mt-1">分析超时、熔断与行锁死锁根因并输出修复代码</div>
              </button>

              <button
                onClick={() => {
                  setDiagnosticTask('NATURAL_TO_SQL');
                  setDiagnosticInput(PRESET_NL_SQL_INPUTS[0]);
                }}
                className={`p-3 rounded-xl border text-left transition ${
                  diagnosticTask === 'NATURAL_TO_SQL'
                    ? 'bg-emerald-950/20 border-emerald-500/60 shadow-sm'
                    : 'bg-slate-950 border-slate-800 hover:border-slate-700'
                }`}
              >
                <div className="text-xs font-bold text-white">自然语言转 ClickHouse SIMD SQL</div>
                <div className="text-[11px] text-slate-400 mt-1">自然语言提问生成列式向量化加速查询与物化视图建议</div>
              </button>

              <button
                onClick={() => {
                  setDiagnosticTask('ARCHITECTURE_REVIEW');
                  setDiagnosticInput(
                    `public class OrderController : ControllerBase\n{\n    private readonly AppDbContext _db;\n    public OrderController(AppDbContext db) => _db = db;\n    // 直接在控制器调用 DbContext，未经过 MediatR 与 UnitOfWork\n}`
                  );
                }}
                className={`p-3 rounded-xl border text-left transition ${
                  diagnosticTask === 'ARCHITECTURE_REVIEW'
                    ? 'bg-purple-950/20 border-purple-500/60 shadow-sm'
                    : 'bg-slate-950 border-slate-800 hover:border-slate-700'
                }`}
              >
                <div className="text-xs font-bold text-white">.NET 10 架构规范合规性评审</div>
                <div className="text-[11px] text-slate-400 mt-1">审查 DDD 纯洁性、WorkContext 隔离与 Native AOT 零反射</div>
              </button>
            </div>

            {/* Input area */}
            <div className="space-y-1.5">
              <label className="text-xs font-medium text-slate-300 block">输入待诊断内容或自然语言查询：</label>
              <textarea
                rows={5}
                value={diagnosticInput}
                onChange={(e) => setDiagnosticInput(e.target.value)}
                className="w-full bg-slate-950 border border-slate-800 rounded-xl p-3.5 text-xs text-white placeholder-slate-500 font-mono focus:outline-none focus:border-indigo-500"
              />
            </div>

            <div className="flex items-center justify-between pt-1">
              <div className="text-[11px] text-slate-500 font-mono">
                Prompt 预估 Tokens: ~{Math.round(diagnosticInput.length / 3.5)}
              </div>
              <button
                onClick={handleRunDiagnostic}
                disabled={diagnosticLoading || !diagnosticInput.trim()}
                className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white font-semibold text-xs transition shadow-md shadow-indigo-600/20 disabled:opacity-50"
              >
                {diagnosticLoading ? (
                  <>
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                    正在分析诊断...
                  </>
                ) : (
                  <>
                    <Sparkles className="w-3.5 h-3.5" />
                    执行 AI 智能诊断
                  </>
                )}
              </button>
            </div>
          </div>

          {/* Diagnostic Result Card */}
          {diagnosticResult && (
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
                <div className="flex items-center gap-2">
                  <CheckCircle2 className="w-5 h-5 text-emerald-400" />
                  <h3 className="text-sm font-semibold text-white">诊断分析报告 (Diagnostic Output)</h3>
                </div>
                <div className="flex items-center gap-2 font-mono text-xs">
                  <span className="text-slate-400">耗时: {diagnosticResult.latencyMs}ms</span>
                  <span className="px-2 py-0.5 rounded bg-slate-800 text-indigo-300">
                    Model: {diagnosticResult.model}
                  </span>
                  <button
                    onClick={() => handleCopy(diagnosticResult.response, 'diag_resp')}
                    className="flex items-center gap-1 px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-mono transition"
                  >
                    {copiedKey === 'diag_resp' ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3 text-indigo-400" />}
                    复制结果
                  </button>
                </div>
              </div>

              <div className="p-4 rounded-xl bg-slate-950 font-mono text-xs text-slate-200 whitespace-pre-wrap leading-relaxed overflow-x-auto border border-slate-800">
                {diagnosticResult.response}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Modal: Add New Knowledge Chunk */}
      {showAddChunkModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in">
          <div className="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-2xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden">
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-950">
              <h3 className="text-base font-bold text-white flex items-center gap-2">
                <Plus className="w-4 h-4 text-indigo-400" />
                新增架构知识切片至 RAG 向量库
              </h3>
              <button
                onClick={() => setShowAddChunkModal(false)}
                className="text-slate-400 hover:text-white transition"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleAddChunk} className="p-6 space-y-4 overflow-y-auto flex-1 text-xs">
              <div className="space-y-1">
                <label className="text-slate-300 font-medium block">切片标题 (Title)</label>
                <input
                  type="text"
                  required
                  placeholder="例如：.NET 10 HybridCache 两级缓存防穿透规范"
                  value={newChunkTitle}
                  onChange={(e) => setNewChunkTitle(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <label className="text-slate-300 font-medium block">所属分类 (Category)</label>
                  <select
                    value={newChunkCategory}
                    onChange={(e) => setNewChunkCategory(e.target.value as any)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white"
                  >
                    <option value="Architecture">Architecture (架构)</option>
                    <option value="Runtime">Runtime (运行时与锁)</option>
                    <option value="Distributed">Distributed (分布式事务)</option>
                    <option value="FaultTolerance">FaultTolerance (容灾熔断)</option>
                    <option value="Database">Database (数仓与存储)</option>
                  </select>
                </div>
                <div className="space-y-1">
                  <label className="text-slate-300 font-medium block">关键词标签 (Tags，逗号分隔)</label>
                  <input
                    type="text"
                    value={newChunkTags}
                    onChange={(e) => setNewChunkTags(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white font-mono"
                  />
                </div>
              </div>

              <div className="space-y-1">
                <label className="text-slate-300 font-medium block">切片知识正文内容 (Content)</label>
                <textarea
                  rows={6}
                  required
                  placeholder="详细描述该微服务知识点的架构原理、实现要点、避坑指南及代码范例..."
                  value={newChunkContent}
                  onChange={(e) => setNewChunkContent(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-white font-sans leading-relaxed"
                />
              </div>

              <div className="pt-3 border-t border-slate-800 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowAddChunkModal(false)}
                  className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 transition"
                >
                  取消
                </button>
                <button
                  type="submit"
                  className="px-5 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white font-medium transition"
                >
                  保存并加入向量索引
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
