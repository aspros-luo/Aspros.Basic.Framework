import React, { useState, useEffect } from 'react';
import {
  Server,
  Zap,
  Database,
  Radio,
  ShieldCheck,
  ShieldAlert,
  Activity,
  BookOpen,
  Send,
  RefreshCw,
  Plus,
  Trash2,
  CheckCircle2,
  AlertTriangle,
  Play,
  RotateCcw,
  Check,
  Layers,
  ArrowRight,
  Clock,
  Building2,
  PackageCheck,
  CreditCard,
  Sliders,
  FileCode2,
  CheckCircle,
  XCircle,
  HelpCircle,
  Sparkles,
  Lock,
  Compass,
} from 'lucide-react';
import type {
  OrderAggregate,
  InventoryEntity,
  PaymentEntity,
  StagedChange,
  ServiceInstance,
  CapOutboxMessage,
  RpcTraceResult,
} from './types';
import { ChainTopologyGraph } from './components/ChainTopologyGraph';
import { FrameworkAuditDashboard } from './components/FrameworkAuditDashboard';
import { FormalVerificationDashboard } from './components/FormalVerificationDashboard';
import { ClickHousePersonalizationDashboard } from './components/ClickHousePersonalizationDashboard';

export default function App() {
  const [activeTab, setActiveTab] = useState<
    'clickhouse' | 'verification' | 'chain' | 'uow' | 'cap' | 'discovery' | 'permission' | 'resilience' | 'architecture' | 'audit'
  >('clickhouse');

  // Multi-tenant selection
  const [currentTenant, setCurrentTenant] = useState<number | null>(null);

  // Microservice Chain / gRPC state
  const [chainMessage, setChainMessage] = useState('ping from consumer gateway');
  const [chainDeadlineMs, setChainDeadlineMs] = useState(500);
  const [simulateTimeout, setSimulateTimeout] = useState(false);
  const [bearerToken, setBearerToken] = useState('aspros-jwt-bearer-token-1001');
  const [chainLoading, setChainLoading] = useState(false);
  const [chainResult, setChainResult] = useState<RpcTraceResult | null>(null);

  // Business / UoW data state
  const [orders, setOrders] = useState<OrderAggregate[]>([]);
  const [inventories, setInventories] = useState<InventoryEntity[]>([]);
  const [payments, setPayments] = useState<PaymentEntity[]>([]);
  const [stagedChanges, setStagedChanges] = useState<StagedChange[]>([]);
  const [uowLoading, setUowLoading] = useState(false);
  const [selectedSku, setSelectedSku] = useState('SKU-PRO-SERVER-16C');
  const [orderQty, setOrderQty] = useState(1);
  const [customerName, setCustomerName] = useState('杭州数智云计算中心');
  const [uowNotice, setUowNotice] = useState<{ type: 'success' | 'error' | 'info'; text: string } | null>(null);

  // Dapper Analytics state
  const [dapperAnalytics, setDapperAnalytics] = useState<any>(null);
  const [dapperLoading, setDapperLoading] = useState(false);

  // CAP Outbox state
  const [capMessages, setCapMessages] = useState<CapOutboxMessage[]>([]);
  const [capTopic, setCapTopic] = useState('aspros.order.dispatched');
  const [capPayloadStr, setCapPayloadStr] = useState('{\n  "orderId": "ORD-20261018-001",\n  "logisticsProvider": "SF_EXPRESS"\n}');
  const [capContractType, setCapContractType] = useState<'IIntegrationEvent' | 'ILocalDomainEvent'>('IIntegrationEvent');
  const [capPublishing, setCapPublishing] = useState(false);

  // Nacos Service Discovery state
  const [instances, setInstances] = useState<ServiceInstance[]>([]);
  const [resolveTargetService, setResolveTargetService] = useState('xr.trade');
  const [resolveEnableZoneAffinity, setResolveEnableZoneAffinity] = useState(true);
  const [resolveCallerZone, setResolveCallerZone] = useState<'zone-east-1a' | 'zone-east-1b'>('zone-east-1a');
  const [resolveResult, setResolveResult] = useState<any>(null);
  const [resolving, setResolving] = useState(false);

  // Permission & WorkContext state
  const [userRole, setUserRole] = useState<'admin' | 'operator' | 'viewer' | 'anonymous'>('operator');
  const [permissionTarget, setPermissionTarget] = useState('trade:write');
  const [permissionResponse, setPermissionResponse] = useState<any>(null);
  const [permLoading, setPermLoading] = useState(false);

  // Resilience state
  const [circuitState, setCircuitState] = useState<'CLOSED' | 'OPEN' | 'HALF_OPEN'>('CLOSED');
  const [resilienceLogs, setResilienceLogs] = useState<Array<{ timestamp: string; status: number; message: string; ok: boolean }>>([]);
  const [resilienceTesting, setResilienceTesting] = useState(false);

  // Optimization Controls state
  const [chainEnableZoneAffinity, setChainEnableZoneAffinity] = useState(true);
  const [chainCallerZone, setChainCallerZone] = useState<'zone-east-1a' | 'zone-east-1b'>('zone-east-1a');
  const [chainEnableIdempotency, setChainEnableIdempotency] = useState(false);
  const [chainIdempotencyKey, setChainIdempotencyKey] = useState(`CHAIN-${Date.now().toString().slice(-4)}`);
  const [chainUseScopeContext, setChainUseScopeContext] = useState(false);

  const [uowEnableIdempotency, setUowEnableIdempotency] = useState(true);
  const [uowIdempotencyKey, setUowIdempotencyKey] = useState(`ORDER-KEY-${Date.now().toString().slice(-4)}`);
  const [uowUseScopeContext, setUowUseScopeContext] = useState(false);
  const [uowScopeCallerType, setUowScopeCallerType] = useState<'CapConsumer' | 'Worker' | 'QuartzJob'>('CapConsumer');

  const [optimizationMetrics, setOptimizationMetrics] = useState<{
    idempotencyInterceptions: number;
    zoneAffinityAccelerations: number;
    totalLatencySavedMs: number;
    scopesDerived: number;
  }>({
    idempotencyInterceptions: 0,
    zoneAffinityAccelerations: 0,
    totalLatencySavedMs: 0,
    scopesDerived: 0,
  });

  // Initial load
  useEffect(() => {
    fetchBusinessData();
    fetchCapMessages();
    fetchInstances();
    runDapperAnalytics();
    fetchOptimizationMetrics();
  }, [currentTenant]);

  const fetchOptimizationMetrics = async () => {
    try {
      const res = await fetch('/api/framework/metrics/optimizations');
      const json = await res.json();
      if (json.success && json.metrics) {
        setOptimizationMetrics(json.metrics);
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handleSelectTenant = (tenant: number | null) => {
    setCurrentTenant(tenant);
    fetch('/api/framework/ck/ingest', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        eventType: 'tenant_context_switch',
        module: 'WorkContext',
        tenantId: tenant || 100,
        latencyMs: 1.1,
      }),
    }).catch(() => {});
  };

  const fetchBusinessData = async () => {
    setUowLoading(true);
    try {
      const url = currentTenant
        ? `/api/framework/business/data?tenantId=${currentTenant}`
        : '/api/framework/business/data';
      const res = await fetch(url);
      const json = await res.json();
      if (json.success) {
        setOrders(json.data.orders);
        setInventories(json.data.inventories);
        setPayments(json.data.payments);
        setStagedChanges(json.data.stagedChanges);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setUowLoading(false);
    }
  };

  const fetchCapMessages = async () => {
    try {
      const res = await fetch('/api/framework/cap/messages');
      const json = await res.json();
      if (json.success) {
        setCapMessages(json.messages);
      }
    } catch (e) {
      console.error(e);
    }
  };

  const fetchInstances = async () => {
    try {
      const res = await fetch('/api/framework/discovery/instances');
      const json = await res.json();
      if (json.success) {
        setInstances(json.instances);
      }
    } catch (e) {
      console.error(e);
    }
  };

  const runDapperAnalytics = async () => {
    setDapperLoading(true);
    try {
      const res = await fetch('/api/framework/dapper/analytics', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tenantId: currentTenant }),
      });
      const json = await res.json();
      setDapperAnalytics(json);
    } catch (e) {
      console.error(e);
    } finally {
      setDapperLoading(false);
    }
  };

  // 1. Invoke Microservice Chain (with Zone-Affinity & Idempotency)
  const handleExecuteChain = async () => {
    setChainLoading(true);
    setChainResult(null);
    try {
      const res = await fetch('/core/microservice-chain', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(chainEnableIdempotency && chainIdempotencyKey ? { 'x-idempotency-key': chainIdempotencyKey } : {}),
        },
        body: JSON.stringify({
          message: chainMessage,
          tenantId: currentTenant || 100,
          bearerToken,
          simulateTimeout,
          deadlineMs: chainDeadlineMs,
          enableZoneAffinity: chainEnableZoneAffinity,
          callerZone: chainCallerZone,
          idempotencyKey: chainEnableIdempotency ? chainIdempotencyKey : undefined,
          useScopeContext: chainUseScopeContext,
        }),
      });
      const data = await res.json();
      setChainResult(data);
      await fetchOptimizationMetrics();
    } catch (err: any) {
      setChainResult({
        traceId: 'err-trace',
        correlationId: 'err-corr',
        tenantId: currentTenant || 100,
        overallDurationMs: 0,
        status: 'DEADLINE_EXCEEDED',
        hops: [],
        finalResponse: { error: err.message },
      });
    } finally {
      setChainLoading(false);
    }
  };

  // 2. Unit of Work Transaction Simulation (with [Idempotent] & WorkContext Scope)
  const handleOrderTransaction = async (simulateFailure: boolean) => {
    setUowLoading(true);
    setUowNotice(null);
    try {
      const res = await fetch('/api/framework/uow/order-transaction', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(uowEnableIdempotency && uowIdempotencyKey ? { 'x-idempotency-key': uowIdempotencyKey } : {}),
        },
        body: JSON.stringify({
          customerName,
          sku: selectedSku,
          quantity: orderQty,
          tenantId: currentTenant || 100,
          simulateFailure,
          idempotencyKey: uowEnableIdempotency ? uowIdempotencyKey : undefined,
          useScopeContext: uowUseScopeContext,
          callerType: uowScopeCallerType,
        }),
      });
      const json = await res.json();
      if (json.hitCache) {
        setUowNotice({
          type: 'info',
          text: `🛡️【[Idempotent] 切面拦截成功】防重标识 (${uowIdempotencyKey}) 60秒内重复提交。已阻断重复库存扣减，直接复用原计算结果！`,
        });
      } else if (json.success) {
        setUowNotice({ type: 'success', text: json.message });
      } else {
        setUowNotice({ type: 'error', text: json.message });
      }
      await fetchBusinessData();
      await fetchCapMessages();
      await runDapperAnalytics();
      await fetchOptimizationMetrics();
    } catch (err: any) {
      setUowNotice({ type: 'error', text: `请求异常: ${err.message}` });
    } finally {
      setUowLoading(false);
    }
  };

  const handleStageSoftDelete = async (orderId: string) => {
    try {
      const res = await fetch('/api/framework/uow/stage-delete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: orderId }),
      });
      const json = await res.json();
      setUowNotice({ type: 'info', text: json.message });
      await fetchBusinessData();
    } catch (err: any) {
      setUowNotice({ type: 'error', text: err.message });
    }
  };

  const handleCommitStaged = async () => {
    setUowLoading(true);
    try {
      const res = await fetch('/api/framework/uow/commit-staged', { method: 'POST' });
      const json = await res.json();
      setUowNotice({ type: 'success', text: json.message });
      await fetchBusinessData();
      await runDapperAnalytics();
    } catch (err: any) {
      setUowNotice({ type: 'error', text: err.message });
    } finally {
      setUowLoading(false);
    }
  };

  const handleRollbackStaged = async () => {
    setUowLoading(true);
    try {
      const res = await fetch('/api/framework/uow/rollback-staged', { method: 'POST' });
      const json = await res.json();
      setUowNotice({ type: 'info', text: json.message });
      await fetchBusinessData();
    } catch (err: any) {
      setUowNotice({ type: 'error', text: err.message });
    } finally {
      setUowLoading(false);
    }
  };

  // 3. CAP Outbox Retry & Publish
  const handleCapRetry = async (msgId: string) => {
    try {
      const res = await fetch('/api/framework/cap/retry', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: msgId }),
      });
      const json = await res.json();
      await fetchCapMessages();
      alert(json.message);
    } catch (err: any) {
      alert(`重试失败: ${err.message}`);
    }
  };

  const handleCapPublish = async (e: React.FormEvent) => {
    e.preventDefault();
    setCapPublishing(true);
    try {
      let parsed = {};
      try {
        parsed = JSON.parse(capPayloadStr);
      } catch {
        parsed = { raw: capPayloadStr };
      }
      await fetch('/api/framework/cap/publish', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          topic: capTopic,
          payload: parsed,
          contractType: capContractType,
        }),
      });
      await fetchCapMessages();
    } catch (e) {
      console.error(e);
    } finally {
      setCapPublishing(false);
    }
  };

  // 4. Discovery Toggle & Resolve (with Zone-Affinity)
  const handleToggleHealth = async (id: string) => {
    try {
      const res = await fetch('/api/framework/discovery/toggle-health', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id }),
      });
      await res.json();
      await fetchInstances();
      // Auto re-resolve to show failover
      handleResolveEndpoint(resolveTargetService);
    } catch (e) {
      console.error(e);
    }
  };

  const handleResolveEndpoint = async (serviceName: string) => {
    setResolving(true);
    setResolveResult(null);
    try {
      const res = await fetch(
        `/api/framework/discovery/resolve?service=${encodeURIComponent(
          serviceName
        )}&callerZone=${resolveCallerZone}&enableZoneAffinity=${resolveEnableZoneAffinity}`
      );
      const data = await res.json();
      setResolveResult(data);
      await fetchOptimizationMetrics();
    } catch (err: any) {
      setResolveResult({ error: err.message });
    } finally {
      setResolving(false);
    }
  };

  // 5. Permission validate
  const handleValidatePermission = async () => {
    setPermLoading(true);
    setPermissionResponse(null);
    try {
      const res = await fetch('/api/framework/permission/validate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userRole,
          requiredPermission: permissionTarget,
          tenantId: currentTenant || 100,
          path: '/system/user.permission.valid',
        }),
      });
      const data = await res.json();
      setPermissionResponse(data);
    } catch (err: any) {
      setPermissionResponse({ error: err.message });
    } finally {
      setPermLoading(false);
    }
  };

  // 6. Resilience test
  const handleTestResilience = async (forceFail: boolean) => {
    setResilienceTesting(true);
    try {
      const res = await fetch(`/api/framework/resilience/test?forceFailure=${forceFail}`);
      const data = await res.json();
      if (data.circuitBreakerState) {
        setCircuitState(data.circuitBreakerState);
      }
      setResilienceLogs((prev) => [
        {
          timestamp: new Date().toLocaleTimeString(),
          status: res.status,
          message: data.message || (data.error ?? '未知响应'),
          ok: res.ok,
        },
        ...prev.slice(0, 9),
      ]);
    } catch (err: any) {
      setResilienceLogs((prev) => [
        {
          timestamp: new Date().toLocaleTimeString(),
          status: 0,
          message: err.message,
          ok: false,
        },
        ...prev.slice(0, 9),
      ]);
    } finally {
      setResilienceTesting(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-blue-600 selection:text-white">
      {/* Top Header */}
      <header className="border-b border-slate-800 bg-slate-900/90 backdrop-blur sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-700 to-indigo-600 flex items-center justify-center font-bold text-white shadow-lg shadow-blue-500/20 text-lg">
              A
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="font-semibold text-lg tracking-tight text-white">Aspros Basic Framework</h1>
                <span className="text-[11px] font-mono px-2 py-0.5 rounded-full bg-indigo-500/10 text-indigo-400 border border-indigo-500/25">
                  .NET 10 (net10.0 LTS) Native
                </span>
              </div>
              <p className="text-xs text-slate-400">轻量级微服务基础设施演练与业务用例工作台</p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* Multi-Tenant Switcher */}
            <div className="flex items-center bg-slate-950 p-1 rounded-lg border border-slate-800 text-xs">
              <span className="text-slate-400 px-2 flex items-center gap-1.5 font-medium">
                <Building2 className="w-3.5 h-3.5 text-blue-400" />
                租户隔离:
              </span>
              <button
                onClick={() => handleSelectTenant(null)}
                className={`px-2.5 py-1 rounded-md text-xs font-medium transition ${
                  currentTenant === null
                    ? 'bg-blue-600 text-white shadow-sm'
                    : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                全局 (全部)
              </button>
              <button
                onClick={() => handleSelectTenant(100)}
                className={`px-2.5 py-1 rounded-md text-xs font-medium transition ${
                  currentTenant === 100
                    ? 'bg-blue-600 text-white shadow-sm'
                    : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                华东租户 (100)
              </button>
              <button
                onClick={() => handleSelectTenant(200)}
                className={`px-2.5 py-1 rounded-md text-xs font-medium transition ${
                  currentTenant === 200
                    ? 'bg-blue-600 text-white shadow-sm'
                    : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                华北租户 (200)
              </button>
            </div>

            <div className="hidden lg:flex items-center gap-2 text-xs bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
              <span className="text-slate-300 font-mono">Port 3000 (0.0.0.0)</span>
            </div>
          </div>
        </div>

        {/* Tab Navigation */}
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex gap-1 overflow-x-auto scrollbar-none border-t border-slate-800/60 pt-1">
          <button
            onClick={() => setActiveTab('clickhouse')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-semibold border-b-2 whitespace-nowrap transition ${
              activeTab === 'clickhouse'
                ? 'border-amber-500 text-amber-400 bg-amber-500/10'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <Database className="w-4 h-4 text-amber-400" />
            CK 实时数仓与千人千面
            <span className="text-[10px] bg-amber-500/20 text-amber-300 px-1.5 py-0.5 rounded font-mono">
              CK + Persona
            </span>
          </button>
          <button
            onClick={() => setActiveTab('verification')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-semibold border-b-2 whitespace-nowrap transition ${
              activeTab === 'verification'
                ? 'border-indigo-500 text-indigo-400 bg-indigo-500/10'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <ShieldCheck className="w-4 h-4 text-indigo-400" />
            形式化不变量与混沌验证
            <span className="text-[10px] bg-indigo-500/25 text-indigo-300 px-1.5 py-0.5 rounded font-mono">
              Jepsen Lab
            </span>
          </button>
          <button
            onClick={() => setActiveTab('chain')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-medium border-b-2 whitespace-nowrap transition ${
              activeTab === 'chain'
                ? 'border-blue-500 text-blue-400 bg-blue-500/5'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <Zap className="w-4 h-4" />
            微服务链路追踪与 gRPC
          </button>
          <button
            onClick={() => setActiveTab('uow')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-medium border-b-2 whitespace-nowrap transition ${
              activeTab === 'uow'
                ? 'border-blue-500 text-blue-400 bg-blue-500/5'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <Database className="w-4 h-4" />
            工作单元与事务仿真
          </button>
          <button
            onClick={() => setActiveTab('cap')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-medium border-b-2 whitespace-nowrap transition ${
              activeTab === 'cap'
                ? 'border-blue-500 text-blue-400 bg-blue-500/5'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <Radio className="w-4 h-4" />
            CAP 事务性发件箱 (Outbox)
          </button>
          <button
            onClick={() => setActiveTab('discovery')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-medium border-b-2 whitespace-nowrap transition ${
              activeTab === 'discovery'
                ? 'border-blue-500 text-blue-400 bg-blue-500/5'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <Server className="w-4 h-4" />
            服务发现与故障切换
          </button>
          <button
            onClick={() => setActiveTab('permission')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-medium border-b-2 whitespace-nowrap transition ${
              activeTab === 'permission'
                ? 'border-blue-500 text-blue-400 bg-blue-500/5'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <ShieldCheck className="w-4 h-4" />
            权限中间件与工作上下文
          </button>
          <button
            onClick={() => setActiveTab('resilience')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-medium border-b-2 whitespace-nowrap transition ${
              activeTab === 'resilience'
                ? 'border-blue-500 text-blue-400 bg-blue-500/5'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <Activity className="w-4 h-4" />
            弹性熔断与限流
          </button>
          <button
            onClick={() => setActiveTab('architecture')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-medium border-b-2 whitespace-nowrap transition ${
              activeTab === 'architecture'
                ? 'border-blue-500 text-blue-400 bg-blue-500/5'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <BookOpen className="w-4 h-4" />
            架构契约与指南
          </button>
          <button
            onClick={() => setActiveTab('audit')}
            className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-semibold border-b-2 whitespace-nowrap transition ${
              activeTab === 'audit'
                ? 'border-indigo-500 text-indigo-400 bg-indigo-500/10'
                : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
            }`}
          >
            <ShieldAlert className="w-4 h-4 text-indigo-400" />
            全景架构审查与演进
            <span className="text-[10px] bg-indigo-500/20 text-indigo-300 px-1.5 py-0.5 rounded font-mono">
              Audit
            </span>
          </button>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-4 sm:p-6 lg:p-8 space-y-6">
        {/* Live Architecture Optimizations KPI Dashboard */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-sm">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800 text-xs">
            <div className="flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-emerald-400" />
              <span className="font-semibold text-slate-100">
                4 项架构优化治理运行指标总览 (Architecture Optimizations Live KPI)
              </span>
              <span className="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-[10px] px-2 py-0.5 rounded-full font-mono">
                Native Injected
              </span>
            </div>
            <div className="flex items-center gap-3">
              <span className="text-[11px] text-slate-400 hidden sm:inline">
                Scope上下文 / 声明式幂等 / 就近亲和路由 / 事件隔离
              </span>
              <button
                onClick={fetchOptimizationMetrics}
                className="flex items-center gap-1.5 text-xs text-slate-300 hover:text-white bg-slate-800 px-2.5 py-1 rounded border border-slate-700 transition"
              >
                <RefreshCw className="w-3 h-3" />
                刷新指标
              </button>
            </div>
          </div>

          <div className="mt-3 grid grid-cols-2 lg:grid-cols-4 gap-3 text-xs">
            {/* KPI 1: 就近路由亲和性加速 */}
            <div className="bg-slate-950 p-3 rounded-lg border border-slate-800/80">
              <div className="flex items-center justify-between text-[11px] text-slate-400">
                <span className="flex items-center gap-1.5 text-emerald-400">
                  <Compass className="w-3.5 h-3.5" /> 就近路由命中
                </span>
                <span className="text-[10px] font-mono bg-emerald-500/10 text-emerald-400 px-1.5 py-0.5 rounded">
                  Zone-Affinity
                </span>
              </div>
              <div className="mt-1 text-lg font-bold font-mono text-white">
                {optimizationMetrics.zoneAffinityAccelerations} <span className="text-xs font-normal text-slate-400">次</span>
              </div>
              <div className="text-[10px] text-slate-500 mt-0.5">
                同机房优先直连 (3ms vs 22ms)
              </div>
            </div>

            {/* KPI 2: 累计节约网络延时 */}
            <div className="bg-slate-950 p-3 rounded-lg border border-slate-800/80">
              <div className="flex items-center justify-between text-[11px] text-slate-400">
                <span className="flex items-center gap-1.5 text-blue-400">
                  <Clock className="w-3.5 h-3.5" /> 累计节省网络延时
                </span>
                <span className="text-[10px] font-mono bg-blue-500/10 text-blue-400 px-1.5 py-0.5 rounded">
                  Latency Saved
                </span>
              </div>
              <div className="mt-1 text-lg font-bold font-mono text-blue-400">
                ~{optimizationMetrics.totalLatencySavedMs} <span className="text-xs font-normal text-slate-400">ms</span>
              </div>
              <div className="text-[10px] text-slate-500 mt-0.5">
                跨机房往返时延优化增益
              </div>
            </div>

            {/* KPI 3: [Idempotent] 幂等防重拦截 */}
            <div className="bg-slate-950 p-3 rounded-lg border border-slate-800/80">
              <div className="flex items-center justify-between text-[11px] text-slate-400">
                <span className="flex items-center gap-1.5 text-amber-400">
                  <Lock className="w-3.5 h-3.5" /> 幂等防重拦截
                </span>
                <span className="text-[10px] font-mono bg-amber-500/10 text-amber-400 px-1.5 py-0.5 rounded">
                  [Idempotent]
                </span>
              </div>
              <div className="mt-1 text-lg font-bold font-mono text-amber-400">
                {optimizationMetrics.idempotencyInterceptions} <span className="text-xs font-normal text-slate-400">次</span>
              </div>
              <div className="text-[10px] text-slate-500 mt-0.5">
                阻断重复下单与扣减库存
              </div>
            </div>

            {/* KPI 4: WorkContext Scope 派生 */}
            <div className="bg-slate-950 p-3 rounded-lg border border-slate-800/80">
              <div className="flex items-center justify-between text-[11px] text-slate-400">
                <span className="flex items-center gap-1.5 text-purple-400">
                  <Layers className="w-3.5 h-3.5" /> Scope 隔离上下文
                </span>
                <span className="text-[10px] font-mono bg-purple-500/10 text-purple-400 px-1.5 py-0.5 rounded">
                  AsyncLocal
                </span>
              </div>
              <div className="mt-1 text-lg font-bold font-mono text-purple-400">
                {optimizationMetrics.scopesDerived} <span className="text-xs font-normal text-slate-400">次</span>
              </div>
              <div className="text-[10px] text-slate-500 mt-0.5">
                非 HTTP 场景租户隔离保障
              </div>
            </div>
          </div>
        </div>

        {/* TAB -1: ClickHouse (CK) 实时数仓与千人千面自适应工作台 */}
        {activeTab === 'clickhouse' && <ClickHousePersonalizationDashboard />}

        {/* TAB 0: 形式化不变量验证与混沌测试工作台 */}
        {activeTab === 'verification' && <FormalVerificationDashboard />}

        {/* TAB 1: 微服务链路追踪与 gRPC */}
        {activeTab === 'chain' && (
          <div className="space-y-6">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-4 border-b border-slate-800">
                <div>
                  <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                    <Zap className="w-5 h-5 text-blue-400" />
                    多级微服务链路调用与全链路追踪 (Trace Propagation)
                  </h2>
                  <p className="text-xs text-slate-400 mt-1">
                    演示 Aspros 核心能力：<code className="text-blue-300">ForwardAuthorizationHeader()</code> 透传 Bearer
                    Token，并配合 <code className="text-blue-300">PropagateGrpcCallContext()</code> 级联传递
                    CancellationToken 与 Deadline 超时上下文。
                  </p>
                </div>
                <div className="flex items-center gap-2 text-xs bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700">
                  <span className="text-slate-400">调用目标租户:</span>
                  <span className="font-semibold text-blue-400 font-mono">
                    {currentTenant ? `Tenant ${currentTenant}` : 'Tenant 100 (默认)'}
                  </span>
                </div>
              </div>

              {/* Call Controls */}
              <div className="mt-5 grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">
                    请求消息内容 (Request Message)
                  </label>
                  <input
                    type="text"
                    value={chainMessage}
                    onChange={(e) => setChainMessage(e.target.value)}
                    placeholder="输入需要经由链路发送的文本"
                    className="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-blue-500"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">
                    Bearer Token (将被自动转发至下游 gRPC)
                  </label>
                  <input
                    type="text"
                    value={bearerToken}
                    onChange={(e) => setBearerToken(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-slate-300 focus:outline-none focus:border-blue-500"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">
                    超时控制 (Deadline)
                  </label>
                  <div className="flex items-center gap-2">
                    <select
                      value={chainDeadlineMs}
                      onChange={(e) => setChainDeadlineMs(Number(e.target.value))}
                      className="bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-blue-500 flex-1"
                    >
                      <option value={200}>200 毫秒 (严苛)</option>
                      <option value={500}>500 毫秒 (标准)</option>
                      <option value={2000}>2000 毫秒 (宽裕)</option>
                    </select>

                    <label className="flex items-center gap-1.5 text-xs text-slate-300 bg-slate-950 px-2.5 py-2 rounded-lg border border-slate-700 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={simulateTimeout}
                        onChange={(e) => setSimulateTimeout(e.target.checked)}
                        className="rounded border-slate-700 text-blue-600 focus:ring-0"
                      />
                      <span>模拟超时中断</span>
                    </label>
                  </div>
                </div>
              </div>

              {/* Native Architecture Optimization Feature Toggles */}
              <div className="mt-4 bg-slate-950 p-3.5 rounded-xl border border-slate-800">
                <div className="flex items-center justify-between pb-2 border-b border-slate-800/80 mb-3">
                  <span className="text-xs font-semibold text-slate-200 flex items-center gap-1.5">
                    <Sliders className="w-3.5 h-3.5 text-emerald-400" />
                    架构优化与治理特性注入开关 (Native Architectural Feature Toggles)
                  </span>
                  <span className="text-[10px] text-slate-500 font-mono">Live Toggle & Comparison</span>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
                  {/* Toggle 1: Zone-Affinity */}
                  <div className="bg-slate-900/90 p-3 rounded-lg border border-slate-800 space-y-2">
                    <div className="flex items-center justify-between">
                      <label className="flex items-center gap-2 font-medium text-slate-200 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={chainEnableZoneAffinity}
                          onChange={(e) => setChainEnableZoneAffinity(e.target.checked)}
                          className="rounded border-slate-700 text-emerald-500 focus:ring-0"
                        />
                        <span className="flex items-center gap-1 text-emerald-400">
                          <Compass className="w-3.5 h-3.5" /> 就近路由 (Zone-Affinity)
                        </span>
                      </label>
                      <span className="text-[10px] font-mono text-slate-400">
                        {chainEnableZoneAffinity ? '⚡ 3ms' : '🐢 22ms'}
                      </span>
                    </div>
                    <div>
                      <span className="text-[11px] text-slate-400 block mb-1">发起方所属机房可用区:</span>
                      <select
                        value={chainCallerZone}
                        onChange={(e: any) => setChainCallerZone(e.target.value)}
                        className="w-full bg-slate-950 border border-slate-700 rounded px-2 py-1 text-xs text-slate-300 font-mono"
                      >
                        <option value="zone-east-1a">zone-east-1a (华东1区机房 A)</option>
                        <option value="zone-east-1b">zone-east-1b (华东1区机房 B)</option>
                      </select>
                    </div>
                  </div>

                  {/* Toggle 2: Idempotency */}
                  <div className="bg-slate-900/90 p-3 rounded-lg border border-slate-800 space-y-2">
                    <div className="flex items-center justify-between">
                      <label className="flex items-center gap-2 font-medium text-slate-200 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={chainEnableIdempotency}
                          onChange={(e) => setChainEnableIdempotency(e.target.checked)}
                          className="rounded border-slate-700 text-amber-500 focus:ring-0"
                        />
                        <span className="flex items-center gap-1 text-amber-400">
                          <Lock className="w-3.5 h-3.5" /> 链路幂等拦截 ([Idempotent])
                        </span>
                      </label>
                      <button
                        onClick={() => setChainIdempotencyKey(`CHAIN-${Date.now().toString().slice(-4)}`)}
                        className="text-[10px] text-blue-400 hover:underline"
                      >
                        生成新Key
                      </button>
                    </div>
                    <div>
                      <span className="text-[11px] text-slate-400 block mb-1">防重令牌 (Idempotency Key):</span>
                      <input
                        type="text"
                        value={chainIdempotencyKey}
                        onChange={(e) => setChainIdempotencyKey(e.target.value)}
                        className="w-full bg-slate-950 border border-slate-700 rounded px-2 py-1 text-xs text-slate-300 font-mono"
                      />
                    </div>
                  </div>

                  {/* Toggle 3: WorkContext Scope */}
                  <div className="bg-slate-900/90 p-3 rounded-lg border border-slate-800 space-y-2 flex flex-col justify-between">
                    <div>
                      <div className="flex items-center justify-between">
                        <label className="flex items-center gap-2 font-medium text-slate-200 cursor-pointer">
                          <input
                            type="checkbox"
                            checked={chainUseScopeContext}
                            onChange={(e) => setChainUseScopeContext(e.target.checked)}
                            className="rounded border-slate-700 text-purple-500 focus:ring-0"
                          />
                          <span className="flex items-center gap-1 text-purple-400">
                            <Layers className="w-3.5 h-3.5" /> Scope 隔离上下文
                          </span>
                        </label>
                      </div>
                      <p className="text-[11px] text-slate-400 mt-1 leading-snug">
                        启用后模拟非 HTTP 后台线程，通过 AsyncLocal Scope 承载当前租户 {currentTenant || 100}。
                      </p>
                    </div>
                    <div className="text-[10px] text-slate-500 font-mono">
                      模式: {chainUseScopeContext ? 'IWorkContextScopeHolder' : 'IHttpContextAccessor'}
                    </div>
                  </div>
                </div>
              </div>

              <div className="mt-4 flex justify-end">
                <button
                  onClick={handleExecuteChain}
                  disabled={chainLoading}
                  className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-50 text-white text-xs font-medium py-2 px-5 rounded-lg shadow-sm transition"
                >
                  {chainLoading ? (
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <Send className="w-3.5 h-3.5" />
                  )}
                  发起全链路微服务调用 (Execute Chain)
                </button>
              </div>

              {/* Dynamic SVG Chain Topology DAG */}
              <ChainTopologyGraph chainResult={chainResult} />

              {/* Chain Results Output */}
              {chainResult && (
                <div className="mt-5 bg-slate-950 border border-slate-800 rounded-lg p-4 space-y-3">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-2 border-b border-slate-800 text-xs">
                    <div className="flex items-center gap-2">
                      <span
                        className={`px-2 py-0.5 rounded text-[11px] font-bold ${
                          chainResult.status === 'SUCCESS'
                            ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                            : 'bg-rose-500/20 text-rose-400 border border-rose-500/30'
                        }`}
                      >
                        {chainResult.status === 'SUCCESS' ? '链路执行成功 (200 OK)' : '调用超时中断 (DEADLINE EXCEEDED)'}
                      </span>
                      <span className="font-mono text-slate-400">耗时: {chainResult.overallDurationMs} ms</span>
                    </div>

                    <div className="flex items-center gap-3 text-slate-400 font-mono text-[11px]">
                      <span>TraceId: {chainResult.traceId}</span>
                      <span>CorrelationId: {chainResult.correlationId}</span>
                    </div>
                  </div>

                  {/* Step by step hop logs */}
                  <div className="space-y-1.5">
                    {chainResult.hops.map((h, i) => (
                      <div
                        key={i}
                        className="bg-slate-900/80 p-2.5 rounded border border-slate-800/80 text-xs flex items-center justify-between"
                      >
                        <div className="flex items-center gap-2">
                          <span className="w-5 h-5 rounded-full bg-slate-800 text-slate-300 font-mono flex items-center justify-center text-[10px]">
                            {i + 1}
                          </span>
                          <span className="font-semibold text-slate-200">{h.service}</span>
                          <span className="text-[10px] text-slate-400">({h.protocol})</span>
                          <span className="text-slate-400">— {h.action}</span>
                        </div>
                        <div className="flex items-center gap-3">
                          <span className="text-slate-500 text-[10px] font-mono">
                            {JSON.stringify(h.headersPassed)}
                          </span>
                          <span
                            className={`font-mono text-[11px] ${
                              h.status === 'OK' ? 'text-emerald-400' : 'text-rose-400 font-bold'
                            }`}
                          >
                            {h.durationMs}ms [{h.status}]
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>

                  {/* Optimization Gains & Comparison Card */}
                  {chainResult.optimizations && (
                    <div className="bg-slate-900/90 rounded-lg p-3 border border-slate-800 space-y-2">
                      <div className="flex items-center justify-between text-xs font-semibold text-slate-200">
                        <span className="flex items-center gap-1.5 text-emerald-400">
                          <Sparkles className="w-3.5 h-3.5" />
                          架构优化收益与治理对比分析 (Optimization Gains & Diagnostics)
                        </span>
                        <span className="text-[10px] text-slate-400 font-mono">Real-time Verified</span>
                      </div>

                      <div className="grid grid-cols-1 md:grid-cols-3 gap-2.5 text-xs">
                        {/* Gain 1: Zone Affinity */}
                        <div
                          className={`p-2.5 rounded border ${
                            chainResult.optimizations.zoneAffinityApplied
                              ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                              : 'bg-slate-950 border-slate-800 text-slate-400'
                          }`}
                        >
                          <div className="flex items-center justify-between font-semibold text-[11px] mb-1">
                            <span className="flex items-center gap-1">
                              <Compass className="w-3 h-3" /> 就近亲和路由
                            </span>
                            <span className="font-mono text-[10px]">
                              {chainResult.optimizations.zoneAffinityApplied ? '⚡ 同机房命中' : '🐢 跨机房'}
                            </span>
                          </div>
                          <p className="text-[11px] leading-snug">
                            {chainResult.optimizations.zoneAffinityApplied
                              ? `调用方 (${chainResult.optimizations.callerZone}) -> 目标 (${chainResult.optimizations.targetZone})，耗时 3ms，已节约 ~${chainResult.optimizations.latencySavedMs}ms 跨机房网络延迟。`
                              : `跨机房通信 (${chainResult.optimizations.callerZone} -> ${chainResult.optimizations.targetZone})，网络开销 ~22ms。建议开启就近路由。`}
                          </p>
                        </div>

                        {/* Gain 2: Idempotency Interception */}
                        <div
                          className={`p-2.5 rounded border ${
                            (chainResult.optimizations as any).hitCache
                              ? 'bg-amber-500/10 border-amber-500/30 text-amber-300'
                              : chainResult.optimizations.idempotencyApplied
                              ? 'bg-blue-500/10 border-blue-500/30 text-blue-300'
                              : 'bg-slate-950 border-slate-800 text-slate-400'
                          }`}
                        >
                          <div className="flex items-center justify-between font-semibold text-[11px] mb-1">
                            <span className="flex items-center gap-1">
                              <Lock className="w-3 h-3" /> 声明式幂等切面
                            </span>
                            <span className="font-mono text-[10px]">
                              {(chainResult.optimizations as any).hitCache
                                ? '🛡️ 重复拦截命中'
                                : chainResult.optimizations.idempotencyApplied
                                ? '🔒 首次锁定生效'
                                : '未开启'}
                            </span>
                          </div>
                          <p className="text-[11px] leading-snug">
                            {(chainResult.optimizations as any).hitCache
                              ? (chainResult.optimizations as any).message ||
                                '检测到重复调用，已跳过下游 RPC 执行并直接复用计算结果！'
                              : chainResult.optimizations.idempotencyApplied
                              ? `防重 Key (${chainResult.optimizations.idempotencyKey}) 建立 60 秒锁保护。再次点击即可触发拦截。`
                              : '未启用链路级幂等防重 Key，重复请求将继续向各级微服务穿透。'}
                          </p>
                        </div>

                        {/* Gain 3: Scope Context */}
                        <div
                          className={`p-2.5 rounded border ${
                            chainResult.optimizations.scopeContextApplied
                              ? 'bg-purple-500/10 border-purple-500/30 text-purple-300'
                              : 'bg-slate-950 border-slate-800 text-slate-400'
                          }`}
                        >
                          <div className="flex items-center justify-between font-semibold text-[11px] mb-1">
                            <span className="flex items-center gap-1">
                              <Layers className="w-3 h-3" /> WorkContext 上下文
                            </span>
                            <span className="font-mono text-[10px]">
                              {chainResult.optimizations.scopeContextApplied ? 'Scope 派生' : 'HTTP Claims'}
                            </span>
                          </div>
                          <p className="text-[11px] leading-snug">
                            {chainResult.optimizations.scopeContextApplied
                              ? `当前为非 HTTP 异步线程安全模式，通过 AsyncLocal Scope 承载租户 Tenant ${chainResult.tenantId}。`
                              : `使用传统 IHttpContextAccessor 从请求头解析租户 ${chainResult.tenantId}。`}
                          </p>
                        </div>
                      </div>
                    </div>
                  )}

                  <div className="bg-slate-900 p-3 rounded border border-slate-800">
                    <div className="text-[11px] text-slate-400 font-semibold mb-1">下游返回载荷 (Payload):</div>
                    <pre className="font-mono text-xs text-slate-300 overflow-x-auto">
                      {JSON.stringify(chainResult.finalResponse, null, 2)}
                    </pre>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* TAB 2: 工作单元与事务仿真 */}
        {activeTab === 'uow' && (
          <div className="space-y-6">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-4 border-b border-slate-800">
                <div>
                  <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                    <Database className="w-5 h-5 text-indigo-400" />
                    UnitOfWork 跨实体复杂原子事务与回滚实验
                  </h2>
                  <p className="text-xs text-slate-400 mt-1">
                    演示 Aspros 原则：<code className="text-indigo-300">UnitOfWork</code> 绝不在{' '}
                    <code className="text-indigo-300">RegisterNew/Dirty/Deleted</code> 内部隐式 SaveChanges。必须调用{' '}
                    <code className="text-indigo-300">CommitAsync()</code> 显式提交；异常时调用{' '}
                    <code className="text-indigo-300">RollbackAsync()</code> 彻底恢复。
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={handleRollbackStaged}
                    disabled={stagedChanges.length === 0 || uowLoading}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 disabled:opacity-40 transition"
                  >
                    <RotateCcw className="w-3.5 h-3.5" />
                    RollbackAsync ({stagedChanges.length})
                  </button>
                  <button
                    onClick={handleCommitStaged}
                    disabled={stagedChanges.length === 0 || uowLoading}
                    className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg text-xs font-semibold bg-indigo-600 hover:bg-indigo-500 text-white shadow-sm disabled:opacity-40 transition"
                  >
                    <Check className="w-3.5 h-3.5" />
                    CommitAsync ({stagedChanges.length})
                  </button>
                </div>
              </div>

              {uowNotice && (
                <div
                  className={`mt-4 p-3 rounded-lg border text-xs flex items-center justify-between ${
                    uowNotice.type === 'success'
                      ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                      : uowNotice.type === 'error'
                      ? 'bg-rose-500/10 border-rose-500/30 text-rose-300'
                      : 'bg-blue-500/10 border-blue-500/30 text-blue-300'
                  }`}
                >
                  <span>{uowNotice.text}</span>
                  <button onClick={() => setUowNotice(null)} className="text-slate-400 hover:text-white">✕</button>
                </div>
              )}

              {/* Order Transaction Simulator Form */}
              <div className="mt-5 bg-slate-950 p-4 rounded-xl border border-slate-800">
                <h3 className="text-xs font-semibold text-slate-200 uppercase tracking-wider mb-3 flex items-center gap-2">
                  <PackageCheck className="w-4 h-4 text-indigo-400" />
                  业务场景仿真：创建订单聚合 + 同步扣减库存 (跨表原子操作)
                </h3>

                <div className="grid grid-cols-1 sm:grid-cols-4 gap-3 items-end">
                  <div>
                    <label className="block text-xs text-slate-400 mb-1">采购企业客户名称</label>
                    <input
                      type="text"
                      value={customerName}
                      onChange={(e) => setCustomerName(e.target.value)}
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
                    />
                  </div>

                  <div>
                    <label className="block text-xs text-slate-400 mb-1">选择物料 SKU</label>
                    <select
                      value={selectedSku}
                      onChange={(e) => setSelectedSku(e.target.value)}
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
                    >
                      {inventories.map((inv) => (
                        <option key={inv.sku} value={inv.sku}>
                          {inv.name} (库存: {inv.availableStock})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs text-slate-400 mb-1">采购件数</label>
                    <input
                      type="number"
                      min={1}
                      max={50}
                      value={orderQty}
                      onChange={(e) => setOrderQty(Math.max(1, Number(e.target.value)))}
                      className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
                    />
                  </div>

                  <div className="flex gap-2">
                    <button
                      onClick={() => handleOrderTransaction(false)}
                      disabled={uowLoading}
                      className="flex-1 bg-indigo-600 hover:bg-indigo-500 text-white font-medium py-2 px-3 rounded-lg text-xs shadow-sm transition"
                    >
                      正常原子提交
                    </button>
                    <button
                      onClick={() => handleOrderTransaction(true)}
                      disabled={uowLoading}
                      className="bg-rose-600/80 hover:bg-rose-600 text-white font-medium py-2 px-3 rounded-lg text-xs shadow-sm transition"
                      title="模拟在事务提交过程中发生库存锁定超限或异常，触发 RollbackAsync"
                    >
                      模拟故障回滚
                    </button>
                  </div>
                </div>

                {/* Optimization Controls for UoW */}
                <div className="mt-4 pt-3 border-t border-slate-800/80 grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                  {/* UoW Opt 1: Idempotency */}
                  <div className="bg-slate-900/90 p-3 rounded-lg border border-slate-800 space-y-2">
                    <div className="flex items-center justify-between">
                      <label className="flex items-center gap-2 font-medium text-slate-200 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={uowEnableIdempotency}
                          onChange={(e) => setUowEnableIdempotency(e.target.checked)}
                          className="rounded border-slate-700 text-amber-500 focus:ring-0"
                        />
                        <span className="flex items-center gap-1 text-amber-400">
                          <Lock className="w-3.5 h-3.5" /> [Idempotent] 声明式防重切面
                        </span>
                      </label>
                      <button
                        onClick={() => setUowIdempotencyKey(`ORDER-KEY-${Date.now().toString().slice(-4)}`)}
                        className="text-[10px] text-blue-400 hover:underline"
                      >
                        生成新Key
                      </button>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-[11px] text-slate-400 shrink-0">防重 Key:</span>
                      <input
                        type="text"
                        value={uowIdempotencyKey}
                        onChange={(e) => setUowIdempotencyKey(e.target.value)}
                        className="flex-1 bg-slate-950 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200 font-mono"
                      />
                    </div>
                    <p className="text-[10.5px] text-slate-400 leading-snug">
                      启用后连续快速点击“正常原子提交”，第二次请求直接命中幂等防重缓存，阻止重复扣减库存。
                    </p>
                  </div>

                  {/* UoW Opt 2: Scope Context */}
                  <div className="bg-slate-900/90 p-3 rounded-lg border border-slate-800 space-y-2 flex flex-col justify-between">
                    <div>
                      <div className="flex items-center justify-between">
                        <label className="flex items-center gap-2 font-medium text-slate-200 cursor-pointer">
                          <input
                            type="checkbox"
                            checked={uowUseScopeContext}
                            onChange={(e) => setUowUseScopeContext(e.target.checked)}
                            className="rounded border-slate-700 text-purple-500 focus:ring-0"
                          />
                          <span className="flex items-center gap-1 text-purple-400">
                            <Layers className="w-3.5 h-3.5" /> WorkContext Scope 异步派生
                          </span>
                        </label>
                        <span className="text-[10px] font-mono text-purple-300 bg-purple-500/10 px-1.5 py-0.5 rounded">
                          Tenant {currentTenant || 100}
                        </span>
                      </div>
                      <div className="mt-2 flex items-center gap-2">
                        <span className="text-[11px] text-slate-400 shrink-0">触发源角色:</span>
                        <select
                          value={uowScopeCallerType}
                          onChange={(e: any) => setUowScopeCallerType(e.target.value)}
                          className="flex-1 bg-slate-950 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200 font-mono"
                        >
                          <option value="CapConsumer">CapConsumer (MQ 消息订阅消费)</option>
                          <option value="Worker">BackgroundWorker (后台计算任务)</option>
                          <option value="QuartzJob">QuartzJob (定时分布式调度)</option>
                        </select>
                      </div>
                    </div>
                    <p className="text-[10.5px] text-slate-400 leading-snug">
                      无 HttpContext 场景下由 AsyncLocal 严格注入租户与操作者上下文，杜绝跨租户污染。
                    </p>
                  </div>
                </div>
              </div>

              {/* Inventory Table */}
              <div className="mt-6">
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-xs font-semibold text-slate-300 uppercase">
                    物料库存表 (Inventory Entities)
                  </h4>
                  <span className="text-[11px] text-slate-400">由 EF Core 与 UnitOfWork 跟踪实体变更</span>
                </div>

                <div className="overflow-x-auto border border-slate-800 rounded-lg">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-slate-950 text-slate-400 border-b border-slate-800">
                      <tr>
                        <th className="py-2.5 px-3">物料 SKU</th>
                        <th className="py-2.5 px-3">品名描述</th>
                        <th className="py-2.5 px-3">归属租户</th>
                        <th className="py-2.5 px-3">可用库存 (Available)</th>
                        <th className="py-2.5 px-3">锁定库存 (Locked)</th>
                        <th className="py-2.5 px-3">安全警戒线</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/60 bg-slate-900/60">
                      {inventories.map((inv) => (
                        <tr key={inv.sku}>
                          <td className="py-2.5 px-3 font-mono text-indigo-400 font-semibold">{inv.sku}</td>
                          <td className="py-2.5 px-3 text-slate-200">{inv.name}</td>
                          <td className="py-2.5 px-3 font-mono text-slate-400">Tenant {inv.tenantId}</td>
                          <td className="py-2.5 px-3 font-bold font-mono text-slate-100">{inv.availableStock}</td>
                          <td className="py-2.5 px-3 font-mono text-amber-400">{inv.lockedStock}</td>
                          <td className="py-2.5 px-3 font-mono text-slate-400">{inv.safetyThreshold}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Order Table */}
              <div className="mt-6">
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-xs font-semibold text-slate-300 uppercase">
                    订单聚合根记录 (Order Aggregates)
                  </h4>
                  <span className="text-[11px] text-slate-400">支持软删除状态追踪 (Deleted = true)</span>
                </div>

                <div className="overflow-x-auto border border-slate-800 rounded-lg">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-slate-950 text-slate-400 border-b border-slate-800">
                      <tr>
                        <th className="py-2.5 px-3">订单号</th>
                        <th className="py-2.5 px-3">客户名称</th>
                        <th className="py-2.5 px-3">租户</th>
                        <th className="py-2.5 px-3">订单总额</th>
                        <th className="py-2.5 px-3">业务状态</th>
                        <th className="py-2.5 px-3">软删除标志</th>
                        <th className="py-2.5 px-3 text-right">操作</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/60 bg-slate-900/60">
                      {orders.map((ord) => (
                        <tr key={ord.id} className={ord.deleted ? 'opacity-50 bg-slate-950/40' : ''}>
                          <td className="py-2.5 px-3 font-mono text-slate-300">{ord.id}</td>
                          <td className="py-2.5 px-3 font-medium text-slate-200">{ord.customerName}</td>
                          <td className="py-2.5 px-3 font-mono text-slate-400">Tenant {ord.tenantId}</td>
                          <td className="py-2.5 px-3 font-mono font-semibold text-emerald-400">
                            ¥{ord.totalAmount.toLocaleString()}
                          </td>
                          <td className="py-2.5 px-3">
                            <span className="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300 border border-slate-700">
                              {ord.status}
                            </span>
                          </td>
                          <td className="py-2.5 px-3">
                            {ord.deleted ? (
                              <span className="text-rose-400 font-semibold text-[11px]">已软删除 (Deleted)</span>
                            ) : (
                              <span className="text-emerald-400 text-[11px]">正常 (Active)</span>
                            )}
                          </td>
                          <td className="py-2.5 px-3 text-right">
                            {!ord.deleted && (
                              <button
                                onClick={() => handleStageSoftDelete(ord.id)}
                                className="text-rose-400 hover:text-rose-300 text-[11px] inline-flex items-center gap-1 hover:underline"
                              >
                                <Trash2 className="w-3 h-3" />
                                软删除 (RegisterDeleted)
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Dapper Multi-Tenant Analytics */}
              <div className="mt-6 bg-slate-950 border border-slate-800 rounded-xl p-4">
                <div className="flex items-center justify-between pb-3 border-b border-slate-800">
                  <div className="flex items-center gap-2">
                    <Database className="w-4 h-4 text-emerald-400" />
                    <h4 className="text-xs font-semibold text-slate-200">
                      IDapperExecutor 多租户复杂报表原生 SQL 聚合查询
                    </h4>
                  </div>
                  <button
                    onClick={runDapperAnalytics}
                    disabled={dapperLoading}
                    className="flex items-center gap-1.5 px-2.5 py-1 text-xs rounded bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
                  >
                    <RefreshCw className={`w-3 h-3 ${dapperLoading ? 'animate-spin' : ''}`} />
                    重新执行 SQL
                  </button>
                </div>

                {dapperAnalytics && (
                  <div className="mt-3 grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div className="md:col-span-1 bg-slate-900 p-3 rounded-lg border border-slate-800 font-mono text-[11px] text-emerald-400">
                      <p className="text-slate-500 mb-1">// Dapper 复用 UnitOfWork 数据库连接:</p>
                      <pre className="text-slate-300 overflow-x-auto whitespace-pre-wrap">
                        {dapperAnalytics.sql}
                      </pre>
                      <p className="text-slate-500 mt-2 text-[10px]">
                        执行耗时: {dapperAnalytics.metrics?.executionTimeMs} ms
                      </p>
                    </div>

                    <div className="md:col-span-2 space-y-2">
                      {dapperAnalytics.tenantBreakdown?.map((t: any) => (
                        <div
                          key={t.tenantId}
                          className="bg-slate-900 p-3 rounded-lg border border-slate-800 flex items-center justify-between text-xs"
                        >
                          <div>
                            <span className="font-semibold text-slate-200">{t.tenantName}</span>
                            <div className="text-slate-400 text-[11px] mt-0.5">
                              有效订单: <span className="text-slate-200 font-mono font-bold">{t.activeOrderCount}</span> 笔 | 软删除: {t.softDeletedCount} 笔
                            </div>
                          </div>
                          <div className="text-right font-mono">
                            <span className="text-emerald-400 font-bold text-sm">¥{t.revenueTotal.toLocaleString()}</span>
                            <div className="text-slate-500 text-[10px]">销售总计 ({t.currency})</div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* TAB 3: CAP 事务性发件箱 */}
        {activeTab === 'cap' && (
          <div className="space-y-6">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
              <div className="pb-4 border-b border-slate-800">
                <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                  <Radio className="w-5 h-5 text-amber-400" />
                  CAP 事务性发件箱 (Transactional Outbox) 与最终一致性
                </h2>
                <p className="text-xs text-slate-400 mt-1">
                  在微服务中，跨服务状态同步不能依赖进程内内存事件。Aspros 使用{' '}
                  <code className="text-amber-300">AddFrameworkCap&lt;TDbContext&gt;()</code> 将业务实体写入与消息发件箱绑定在同一个本地数据库事务中，确保消息“绝不丢失”且“至少投递一次”。
                </p>
              </div>

              {/* Visual Pipeline */}
              <div className="mt-5 bg-slate-950 p-4 rounded-xl border border-slate-800">
                <div className="text-xs font-semibold text-slate-300 uppercase tracking-wider mb-3">
                  CAP 事务投递执行阶段 (Outbox Lifecycle)
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-4 gap-3 text-xs">
                  <div className="bg-slate-900 p-3 rounded-lg border border-slate-800">
                    <span className="text-[10px] text-blue-400 font-bold uppercase block mb-1">Step 1. 本地事务</span>
                    <p className="font-semibold text-slate-200">db.BeginTransaction(cap)</p>
                    <p className="text-slate-400 text-[11px] mt-1">业务数据与发件箱消息原子写入同一个 DB</p>
                  </div>
                  <div className="bg-slate-900 p-3 rounded-lg border border-slate-800">
                    <span className="text-[10px] text-indigo-400 font-bold uppercase block mb-1">Step 2. 发件箱暂存</span>
                    <p className="font-semibold text-slate-200">CapMessages.Add()</p>
                    <p className="text-slate-400 text-[11px] mt-1">状态标记为 Pending / Sent，防止断电丢失</p>
                  </div>
                  <div className="bg-slate-900 p-3 rounded-lg border border-slate-800">
                    <span className="text-[10px] text-amber-400 font-bold uppercase block mb-1">Step 3. 异步投递</span>
                    <p className="font-semibold text-slate-200">RabbitMQ / Kafka</p>
                    <p className="text-slate-400 text-[11px] mt-1">后台轮询推送，支持自动指数退避重试</p>
                  </div>
                  <div className="bg-slate-900 p-3 rounded-lg border border-slate-800">
                    <span className="text-[10px] text-emerald-400 font-bold uppercase block mb-1">Step 4. 幂等消费</span>
                    <p className="font-semibold text-slate-200">[CapSubscribe]</p>
                    <p className="text-slate-400 text-[11px] mt-1">消费者通过订单唯一键去重确认 Consumed</p>
                  </div>
                </div>
              </div>

              {/* Messages Table */}
              <div className="mt-6">
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-xs font-semibold text-slate-300 uppercase">
                    发件箱消息流水 (Outbox Messages) ({capMessages.length})
                  </h3>
                  <button
                    onClick={fetchCapMessages}
                    className="text-xs text-slate-400 hover:text-slate-200 flex items-center gap-1"
                  >
                    <RefreshCw className="w-3 h-3" />
                    刷新发件箱
                  </button>
                </div>

                {/* Dual-Track Event Contract Isolation Banner */}
                <div className="mb-4 bg-slate-950 p-3.5 rounded-lg border border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs">
                  <div className="flex items-center gap-2">
                    <ShieldAlert className="w-4 h-4 text-emerald-400 shrink-0" />
                    <div>
                      <span className="font-semibold text-slate-200">事件契约双轨隔离机制 (Dual-Track Event Contracts)</span>
                      <p className="text-slate-400 text-[11px] mt-0.5">
                        严格区分 <code className="text-emerald-400 font-mono">IIntegrationEvent</code> (跨服务分布式事务消息，强制持久化 Outbox) 与{' '}
                        <code className="text-amber-400 font-mono">ILocalDomainEvent</code> (进程内观察者模式，内存广播，严禁跨进程)。
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 font-mono text-[10px] shrink-0">
                    <span className="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                      IIntegrationEvent: CAP 持久化
                    </span>
                    <span className="px-2 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/20">
                      ILocalDomainEvent: 内存隔离
                    </span>
                  </div>
                </div>

                <div className="overflow-x-auto border border-slate-800 rounded-lg">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-slate-950 text-slate-400 border-b border-slate-800">
                      <tr>
                        <th className="py-2.5 px-3">消息 ID</th>
                        <th className="py-2.5 px-3">Topic (主题)</th>
                        <th className="py-2.5 px-3">契约类型 (Contract)</th>
                        <th className="py-2.5 px-3">订阅消费组 (Group)</th>
                        <th className="py-2.5 px-3">投递状态</th>
                        <th className="py-2.5 px-3">重试次数</th>
                        <th className="py-2.5 px-3">事务 ID</th>
                        <th className="py-2.5 px-3 text-right">操作</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/60 bg-slate-900/60 font-mono text-xs">
                      {capMessages.map((msg) => (
                        <tr key={msg.id}>
                          <td className="py-2.5 px-3 text-slate-300">{msg.id}</td>
                          <td className="py-2.5 px-3 text-amber-400 font-semibold">{msg.topic}</td>
                          <td className="py-2.5 px-3">
                            <span
                              className={`px-1.5 py-0.5 rounded text-[10px] font-mono ${
                                msg.contractType === 'ILocalDomainEvent'
                                  ? 'bg-amber-500/10 text-amber-400 border border-amber-500/30'
                                  : 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30'
                              }`}
                            >
                              {msg.contractType || 'IIntegrationEvent'}
                            </span>
                          </td>
                          <td className="py-2.5 px-3 text-slate-400">{msg.group}</td>
                          <td className="py-2.5 px-3">
                            <span
                              className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                                msg.status === 'Consumed'
                                  ? 'bg-emerald-500/20 text-emerald-400'
                                  : msg.status === 'Sent'
                                  ? 'bg-blue-500/20 text-blue-400'
                                  : 'bg-rose-500/20 text-rose-400'
                              }`}
                            >
                              {msg.status}
                            </span>
                          </td>
                          <td className="py-2.5 px-3 text-slate-400">{msg.retryCount}</td>
                          <td className="py-2.5 px-3 text-slate-500 text-[11px]">{msg.transactionId || '-'}</td>
                          <td className="py-2.5 px-3 text-right">
                            {msg.status !== 'Consumed' && (
                              <button
                                onClick={() => handleCapRetry(msg.id)}
                                className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-amber-300 text-[11px] border border-slate-700"
                              >
                                重试投递
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Manual Publish Form */}
              <div className="mt-6 bg-slate-950 p-4 rounded-xl border border-slate-800">
                <div className="flex items-center justify-between mb-3">
                  <h4 className="text-xs font-semibold text-slate-200">手动发布业务事件并执行契约合规检测</h4>
                  <span className="text-[10px] text-slate-500 font-mono">Event Contract Safety Guardrail</span>
                </div>

                <form onSubmit={handleCapPublish} className="space-y-3">
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <div>
                      <label className="block text-xs text-slate-400 mb-1">Topic 主题</label>
                      <input
                        type="text"
                        value={capTopic}
                        onChange={(e) => setCapTopic(e.target.value)}
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-slate-100 focus:outline-none focus:border-amber-500"
                      />
                    </div>
                    <div>
                      <label className="block text-xs text-slate-400 mb-1">契约模式 (Contract Type)</label>
                      <select
                        value={capContractType}
                        onChange={(e: any) => setCapContractType(e.target.value)}
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-amber-500 font-mono"
                      >
                        <option value="IIntegrationEvent">IIntegrationEvent (跨服务分布式事务 - 推荐)</option>
                        <option value="ILocalDomainEvent">ILocalDomainEvent (进程内轻量事件 - 严禁跨服务)</option>
                      </select>
                    </div>
                    <div>
                      <label className="block text-xs text-slate-400 mb-1">Payload JSON 载荷</label>
                      <input
                        type="text"
                        value={capPayloadStr}
                        onChange={(e) => setCapPayloadStr(e.target.value)}
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-slate-100 focus:outline-none focus:border-amber-500"
                      />
                    </div>
                  </div>

                  {capContractType === 'ILocalDomainEvent' && (
                    <div className="p-3 bg-amber-500/10 border border-amber-500/30 rounded-lg text-xs text-amber-300 flex items-start gap-2">
                      <AlertTriangle className="w-4 h-4 shrink-0 text-amber-400 mt-0.5" />
                      <div>
                        <div className="font-semibold text-amber-200">
                          ⚠️ 架构安全守卫拦截提示 (Architectural Guardrail Warning)
                        </div>
                        <p className="mt-0.5 text-[11px] text-amber-300/90 leading-relaxed">
                          当前选定契约类型为 <code className="font-mono text-white">ILocalDomainEvent</code>。
                          进程内领域事件严禁跨进程/微服务边界投递，一旦容器重启消息将直接丢失且无法重试。
                          生产中若需跨微服务保证最终一致性，框架规范强制要求使用 <code className="font-mono text-emerald-300">IIntegrationEvent</code> 配合本地事务发件箱。
                        </p>
                      </div>
                    </div>
                  )}

                  <div className="flex justify-end">
                    <button
                      type="submit"
                      disabled={capPublishing}
                      className="px-5 py-2 bg-amber-600 hover:bg-amber-500 text-white font-medium rounded-lg text-xs shadow-sm transition"
                    >
                      {capContractType === 'IIntegrationEvent'
                        ? '写入发件箱并通过 CAP 投递'
                        : '以进程内事件广播 (演示守卫)'}
                    </button>
                  </div>
                </form>
              </div>
            </div>
          </div>
        )}

        {/* TAB 4: 服务发现与故障切换 */}
        {activeTab === 'discovery' && (
          <div className="space-y-6">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
              <div className="pb-4 border-b border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                  <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                    <Server className="w-5 h-5 text-emerald-400" />
                    Nacos 服务注册与健康实例动态路由 (Failover Discovery)
                  </h2>
                  <p className="text-xs text-slate-400 mt-1">
                    验证 <code className="text-emerald-300">IServiceDiscovery.GetHealthyEndpointAsync(serviceName)</code>。
                    当某个微服务节点发生故障停机时，服务发现层自动剔除不可用节点，始终将请求路由至存活实例。
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <select
                    value={resolveTargetService}
                    onChange={(e) => {
                      setResolveTargetService(e.target.value);
                      handleResolveEndpoint(e.target.value);
                    }}
                    className="bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-emerald-500 font-mono"
                  >
                    <option value="xr.trade">xr.trade (交易微服务 - 2节点集群)</option>
                    <option value="framework-core-provider">framework-core-provider (gRPC 服务端)</option>
                    <option value="saas-system">saas-system (权限校验中心)</option>
                    <option value="payment.service">payment.service (支付结算服务)</option>
                  </select>

                  <select
                    value={resolveCallerZone}
                    onChange={(e: any) => setResolveCallerZone(e.target.value)}
                    className="bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-emerald-500 font-mono"
                    title="当前发起方部署机房可用区"
                  >
                    <option value="zone-east-1a">调用方: 1a 机房</option>
                    <option value="zone-east-1b">调用方: 1b 机房</option>
                  </select>

                  <label className="flex items-center gap-1.5 text-xs text-slate-300 bg-slate-950 px-2.5 py-1.5 rounded-lg border border-slate-700 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={resolveEnableZoneAffinity}
                      onChange={(e) => setResolveEnableZoneAffinity(e.target.checked)}
                      className="rounded border-slate-700 text-emerald-500 focus:ring-0"
                    />
                    <span className="text-emerald-400 font-medium">就近亲和路由</span>
                  </label>

                  <button
                    onClick={() => handleResolveEndpoint(resolveTargetService)}
                    disabled={resolving}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white shadow-sm transition"
                  >
                    <Play className="w-3.5 h-3.5" />
                    解析健康节点
                  </button>
                </div>
              </div>

              {/* Instances Cards */}
              <div className="mt-5 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {instances.map((inst) => (
                  <div
                    key={inst.id}
                    className={`p-4 rounded-xl border transition ${
                      inst.healthy
                        ? 'bg-slate-950 border-slate-800 hover:border-slate-700'
                        : 'bg-rose-950/20 border-rose-900/60'
                    }`}
                  >
                    <div className="flex items-center justify-between pb-2 border-b border-slate-800/80">
                      <div className="flex items-center gap-2">
                        <span
                          className={`w-2.5 h-2.5 rounded-full ${
                            inst.healthy ? 'bg-emerald-500 shadow-sm shadow-emerald-500/50' : 'bg-rose-500'
                          }`}
                        ></span>
                        <span className="font-semibold text-slate-200 text-xs font-mono">{inst.id}</span>
                      </div>
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-400 font-mono">
                        权重: {inst.weight}
                      </span>
                    </div>

                    <div className="mt-3 space-y-1 text-xs">
                      <div className="text-slate-400 flex justify-between">
                        <span>服务名:</span>
                        <span className="text-slate-200 font-semibold">{inst.serviceName}</span>
                      </div>
                      <div className="text-slate-400 flex justify-between">
                        <span>实例地址:</span>
                        <span className="text-slate-200 font-mono">{inst.ip}:{inst.port}</span>
                      </div>
                      <div className="text-slate-400 flex justify-between">
                        <span>健康状态:</span>
                        <span className={inst.healthy ? 'text-emerald-400 font-bold' : 'text-rose-400 font-bold'}>
                          {inst.healthy ? 'HEALTHY (正常)' : 'UNHEALTHY (故障已剔除)'}
                        </span>
                      </div>
                    </div>

                    <div className="mt-4 pt-3 border-t border-slate-800/80 flex justify-end">
                      <button
                        onClick={() => handleToggleHealth(inst.id)}
                        className={`text-xs px-3 py-1.5 rounded-lg border font-medium transition ${
                          inst.healthy
                            ? 'bg-rose-600/20 border-rose-500/40 text-rose-300 hover:bg-rose-600/30'
                            : 'bg-emerald-600/20 border-emerald-500/40 text-emerald-300 hover:bg-emerald-600/30'
                        }`}
                      >
                        {inst.healthy ? '模拟节点下线/故障' : '恢复节点上线'}
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              {/* Resolve Result Box */}
              {resolveResult && (
                <div className="mt-6 bg-slate-950 p-4 rounded-xl border border-emerald-500/30 text-xs">
                  <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                    <div className="font-semibold text-emerald-400 flex items-center gap-2">
                      <CheckCircle2 className="w-4 h-4" />
                      服务发现解析结果 (Resolved ServiceEndpoint)
                    </div>
                    {resolveResult.endpoint && (
                      <span className="text-slate-400 font-mono text-[11px]">
                        可用存活节点: {resolveResult.endpoint.activeNodesInPool} / {resolveResult.endpoint.totalNodesInPool}
                      </span>
                    )}
                  </div>

                  {resolveResult.endpoint ? (
                    <div className="mt-3 space-y-3">
                      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                        <div className="bg-slate-900 p-3 rounded-lg border border-slate-800">
                          <span className="text-[10px] text-slate-400 uppercase block">命中实例地址 (Address)</span>
                          <span className="text-emerald-400 font-mono font-bold text-sm">
                            {resolveResult.endpoint.address}
                          </span>
                        </div>
                        <div className="bg-slate-900 p-3 rounded-lg border border-slate-800">
                          <span className="text-[10px] text-slate-400 uppercase block">实例 ID (InstanceId)</span>
                          <span className="text-slate-200 font-mono font-semibold">
                            {resolveResult.endpoint.instanceId}
                          </span>
                        </div>
                        <div className="bg-slate-900 p-3 rounded-lg border border-slate-800">
                          <span className="text-[10px] text-slate-400 uppercase block">部署区域 (Zone)</span>
                          <span className="text-slate-300 font-mono">{resolveResult.endpoint.zone}</span>
                        </div>
                      </div>

                      {resolveResult.endpoint.zoneAffinity && (
                        <div
                          className={`p-3 rounded-lg border text-xs flex items-center justify-between ${
                            resolveResult.endpoint.zoneAffinity.isAffinityHit
                              ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                              : 'bg-amber-500/10 border-amber-500/30 text-amber-300'
                          }`}
                        >
                          <div className="flex items-center gap-2">
                            <Compass className="w-4 h-4 shrink-0" />
                            <div>
                              <span className="font-semibold">
                                {resolveResult.endpoint.zoneAffinity.isAffinityHit
                                  ? '⚡ 就近亲和路由命中 (Same-Zone Hit)'
                                  : '🐢 跨可用区降级路由 (Cross-Zone Fallback)'}
                              </span>
                              <p className="text-[11px] text-slate-300 mt-0.5">
                                调用方 ({resolveResult.endpoint.zoneAffinity.callerZone}) 与目标节点 (
                                {resolveResult.endpoint.zoneAffinity.targetZone}) 匹配。
                                预估网络延时: {resolveResult.endpoint.zoneAffinity.estimatedLatencyMs} ms
                                {resolveResult.endpoint.zoneAffinity.latencySavedMs > 0 &&
                                  `（已节约 ${resolveResult.endpoint.zoneAffinity.latencySavedMs} ms 网络往返）`}
                              </p>
                            </div>
                          </div>
                          <span className="font-mono text-xs font-bold px-2 py-0.5 rounded bg-slate-900 border border-slate-700">
                            ~{resolveResult.endpoint.zoneAffinity.estimatedLatencyMs}ms
                          </span>
                        </div>
                      )}
                    </div>
                  ) : (
                    <div className="mt-3 text-rose-400 font-semibold bg-rose-500/10 p-3 rounded border border-rose-500/20">
                      {resolveResult.message || resolveResult.error}
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        {/* TAB 5: 权限中间件与工作上下文 */}
        {activeTab === 'permission' && (
          <div className="space-y-6">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
              <div className="pb-4 border-b border-slate-800">
                <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                  <ShieldCheck className="w-5 h-5 text-purple-400" />
                  Aspros 权限校验中间件与 Fail-Closed 原则
                </h2>
                <p className="text-xs text-slate-400 mt-1">
                  框架权限校验遵循闭环失败（Fail-Closed）原则：身份缺失返回 <code className="text-purple-300">401 Unauthorized</code>，
                  无对应权限点返回 <code className="text-purple-300">403 Forbidden</code>，鉴权服务不可用返回{' '}
                  <code className="text-purple-300">503 Service Unavailable</code>。绝不放行未知权限请求。
                </p>
              </div>

              <div className="mt-5 grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-4">
                  <div>
                    <label className="block text-xs font-medium text-slate-300 mb-1.5">
                      模拟当前调用者身份角色 (WorkContext.User)
                    </label>
                    <div className="grid grid-cols-4 gap-2">
                      {(['admin', 'operator', 'viewer', 'anonymous'] as const).map((r) => (
                        <button
                          key={r}
                          onClick={() => setUserRole(r)}
                          className={`py-2 text-xs rounded-lg border font-medium capitalize transition ${
                            userRole === r
                              ? 'bg-purple-600/20 border-purple-500 text-purple-300 shadow-sm'
                              : 'bg-slate-950 border-slate-800 text-slate-400 hover:text-slate-200'
                          }`}
                        >
                          {r}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-slate-300 mb-1.5">
                      请求目标动作权限点 (Required Permission)
                    </label>
                    <select
                      value={permissionTarget}
                      onChange={(e) => setPermissionTarget(e.target.value)}
                      className="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-purple-500"
                    >
                      <option value="trade:read">trade:read (查看交易记录 - 普通权限)</option>
                      <option value="trade:write">trade:write (提交新交易/修改 - 核心权限)</option>
                      <option value="order:create">order:create (创建业务订单)</option>
                      <option value="db:migrate">db:migrate (EF Core 数据库迁移 CLI - 管理员权限)</option>
                      <option value="system:all">system:all (系统超级特权)</option>
                    </select>
                  </div>

                  <button
                    onClick={handleValidatePermission}
                    disabled={permLoading}
                    className="w-full flex items-center justify-center gap-2 bg-purple-600 hover:bg-purple-500 text-white font-medium py-2.5 px-4 rounded-lg text-xs shadow-sm transition"
                  >
                    {permLoading ? (
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <ShieldCheck className="w-3.5 h-3.5" />
                    )}
                    执行权限中间件校验
                  </button>
                </div>

                {/* Result output */}
                <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
                  <div>
                    <div className="text-xs font-semibold text-slate-400 pb-2 border-b border-slate-800 mb-3">
                      中间件拦截校验结果 (Middleware Interception)
                    </div>

                    {permissionResponse ? (
                      <div className="space-y-3 text-xs">
                        <div
                          className={`p-3 rounded-lg border font-semibold flex items-center gap-2 ${
                            permissionResponse.success
                              ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                              : 'bg-rose-500/10 border-rose-500/30 text-rose-300'
                          }`}
                        >
                          {permissionResponse.success ? (
                            <CheckCircle className="w-4 h-4 shrink-0" />
                          ) : (
                            <XCircle className="w-4 h-4 shrink-0" />
                          )}
                          <span>HTTP {permissionResponse.status}: {permissionResponse.message}</span>
                        </div>

                        {permissionResponse.workContext && (
                          <div className="bg-slate-900 p-3 rounded border border-slate-800">
                            <span className="text-[11px] text-slate-400 font-semibold block mb-1">
                              注入的 WorkContext 上下文信息:
                            </span>
                            <pre className="font-mono text-[11px] text-slate-300 overflow-x-auto">
                              {JSON.stringify(permissionResponse.workContext, null, 2)}
                            </pre>
                          </div>
                        )}
                      </div>
                    ) : (
                      <div className="py-12 text-center text-slate-500 text-xs">
                        点击左侧按钮执行权限中间件拦截仿真
                      </div>
                    )}
                  </div>

                  <div className="text-[11px] text-slate-500 pt-3 border-t border-slate-800/80">
                    中间件规则：下游微服务无需关心鉴权细节，统一由 WorkContext 获取 UserId、TenantId 与授权信息。
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 6: 弹性熔断与限流 */}
        {activeTab === 'resilience' && (
          <div className="space-y-6">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm">
              <div className="pb-4 border-b border-slate-800">
                <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                  <Activity className="w-5 h-5 text-rose-400" />
                  微服务高可用：出站 HTTP 弹性 (Polly) 与入站固定窗口限流
                </h2>
                <p className="text-xs text-slate-400 mt-1">
                  验证 <code className="text-rose-300">AddFrameworkResilientHttpClient()</code> (超时、重试、断路器) 与{' '}
                  <code className="text-rose-300">AddFrameworkRateLimiting()</code>。
                </p>
              </div>

              <div className="mt-5 grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                  <span className="text-xs text-slate-400 block mb-1">断路器状态 (Circuit Breaker)</span>
                  <div className="flex items-center gap-2">
                    <span
                      className={`w-3.5 h-3.5 rounded-full ${
                        circuitState === 'CLOSED'
                          ? 'bg-emerald-500 shadow-sm shadow-emerald-500/50'
                          : circuitState === 'HALF_OPEN'
                          ? 'bg-amber-500'
                          : 'bg-rose-500 animate-ping'
                      }`}
                    ></span>
                    <span className="text-lg font-bold font-mono text-slate-100">{circuitState}</span>
                  </div>
                  <p className="text-[11px] text-slate-500 mt-2">
                    {circuitState === 'CLOSED'
                      ? '正常闭合：流量通常通过'
                      : circuitState === 'HALF_OPEN'
                      ? '半开试探：下游恢复中'
                      : '已熔断开启：直接阻断请求，返回 503'}
                  </p>
                </div>

                <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                  <span className="text-xs text-slate-400 block mb-1">入站限流配额 (Rate Limit)</span>
                  <div className="text-lg font-bold font-mono text-slate-100">10 次 / 10 秒</div>
                  <p className="text-[11px] text-slate-500 mt-2">
                    FixedWindowLimiter 保护微服务免遭突发流量压垮
                  </p>
                </div>

                <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col justify-center gap-2">
                  <button
                    onClick={() => handleTestResilience(false)}
                    disabled={resilienceTesting}
                    className="w-full py-2 px-3 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition"
                  >
                    发起正常出站调用 (Normal)
                  </button>
                  <button
                    onClick={() => handleTestResilience(true)}
                    disabled={resilienceTesting}
                    className="w-full py-2 px-3 text-xs font-semibold rounded-lg bg-rose-600/80 hover:bg-rose-600 text-white transition"
                  >
                    模拟下游 500 异常 (连续3次触发熔断)
                  </button>
                </div>
              </div>

              {/* Logs */}
              <div className="mt-5 bg-slate-950 border border-slate-800 rounded-xl p-4">
                <div className="text-xs text-slate-400 font-semibold mb-2">执行拦截与监控日志:</div>
                <div className="space-y-1.5 font-mono text-xs max-h-52 overflow-y-auto">
                  {resilienceLogs.length > 0 ? (
                    resilienceLogs.map((log, i) => (
                      <div
                        key={i}
                        className={`p-2 rounded border flex items-center justify-between text-xs ${
                          log.ok
                            ? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-300'
                            : 'bg-rose-500/10 border-rose-500/20 text-rose-300'
                        }`}
                      >
                        <span>
                          [{log.timestamp}] HTTP {log.status}: {log.message}
                        </span>
                        <span className="font-bold text-[10px]">{log.ok ? 'SUCCESS' : 'BLOCKED/FAIL'}</span>
                      </div>
                    ))
                  ) : (
                    <div className="text-slate-500 text-center py-6">点击上方按钮发起调用并观察日志。</div>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 7: 架构契约与指南 */}
        {activeTab === 'architecture' && (
          <div className="space-y-6">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-sm space-y-6">
              <div className="pb-4 border-b border-slate-800">
                <h2 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                  <Layers className="w-5 h-5 text-indigo-400" />
                  Aspros Basic Framework 架构全景与 .NET 10 LTS 最佳实践
                  <span className="text-[10px] font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 px-2 py-0.5 rounded">
                    net10.0 • C# 14
                  </span>
                </h2>
                <p className="text-xs text-slate-400 mt-1">
                  面向高并发业务的规范化轻量级架构：坚守清晰分层、绝不偷偷提交事务、显式按需装配能力，原生集成 .NET 10 HybridCache 与 Native AOT。
                </p>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
                <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                  <h4 className="font-semibold text-blue-400 text-xs uppercase mb-3 flex items-center gap-1.5">
                    <FileCode2 className="w-4 h-4" />
                    1. 依赖倒置与单向分层
                  </h4>
                  <div className="font-mono text-xs text-slate-300 space-y-2 bg-slate-900 p-3 rounded-lg border border-slate-800 text-center">
                    <div>Aspros.Base.Framework.Abstractions</div>
                    <div className="text-blue-400 text-xs">▲ 最小通用契约 (net10.0)</div>
                    <div>Aspros.Base.Framework.Domain</div>
                    <div className="text-blue-400 text-xs">▲ 领域模型与仓储契约 (net10.0)</div>
                    <div>Aspros.Base.Framework.Infrastructure</div>
                  </div>
                  <p className="text-[11px] text-slate-400 mt-3 leading-relaxed">
                    Domain 严禁反向依赖 Infrastructure；业务微服务仅需依赖 Domain 接口与必要基础设施，纯净支持 Native AOT 零裁剪。
                  </p>
                </div>

                <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                  <h4 className="font-semibold text-indigo-400 text-xs uppercase mb-3 flex items-center gap-1.5">
                    <Database className="w-4 h-4" />
                    2. UnitOfWork 核心准则
                  </h4>
                  <ul className="text-xs text-slate-300 space-y-2 list-disc list-inside leading-relaxed">
                    <li>禁止在 RegisterNew / RegisterDirty 中偷偷执行 SaveChanges。</li>
                    <li>多实体写操作需通过显式 <code className="text-indigo-300">CommitAsync()</code> 提交。</li>
                    <li>EF Core 与 Dapper 共享相同底层数据库连接与当前事务。</li>
                    <li>支持 .NET 10 <code className="text-emerald-300">System.Threading.Lock</code> 守护内部并发状态机。</li>
                  </ul>
                </div>

                <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                  <h4 className="font-semibold text-emerald-400 text-xs uppercase mb-3 flex items-center gap-1.5">
                    <CheckCircle2 className="w-4 h-4" />
                    3. 一行代码极简启动 (.NET 10)
                  </h4>
                  <div className="bg-slate-900 p-3 rounded-lg font-mono text-[11px] text-slate-300 border border-slate-800">
                    <span className="text-slate-500">// .NET 10 LTS 原生启动注册</span>
                    <br />
                    builder.Services.<span className="text-blue-400">AddAsprosFramework</span>(opt =&gt; &#123;
                    <br />
                    &nbsp;&nbsp;opt.<span className="text-indigo-300">UseDotNet10Defaults</span>();
                    <br />
                    &#125;);
                    <br />
                    <br />
                    <span className="text-slate-500">// .NET 10 原生混合两级缓存</span>
                    <br />
                    builder.Services.<span className="text-teal-400">AddFrameworkHybridCache</span>();
                    <br />
                    builder.Services.<span className="text-emerald-400">AddFrameworkGrpc</span>();
                    <br />
                    builder.Services.<span className="text-purple-400">AddFrameworkServiceDiscovery</span>();
                    <br />
                    builder.Services.<span className="text-amber-400">AddFrameworkCap</span>&lt;AppDbContext&gt;();
                  </div>
                  <p className="text-[11px] text-slate-400 mt-3 leading-relaxed">
                    特性按需加载，杜绝无边界装配导致的依赖污染与冷启动拖慢。
                  </p>
                </div>
              </div>

              {/* Migration CLI Guidance */}
              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                <h4 className="font-semibold text-slate-200 text-xs mb-2">
                  Code First 数据库迁移规范 (Aspros.Basic.Framework.Tools on .NET 10)
                </h4>
                <p className="text-xs text-slate-400 mb-3">
                  严格遵循：Entity/Aggregate ➔ 显式 Mapping ➔ DbContext ➔ Migration ➔ 生成幂等 SQL 脚本。
                </p>
                <div className="font-mono text-[11px] bg-slate-900 p-3 rounded-lg text-slate-300 space-y-1 border border-slate-800">
                  <p className="text-slate-500"># 1. 安装全局 .NET 10 CLI 脚手架工具:</p>
                  <p>dotnet tool install --global Aspros.Basic.Framework.Tools --framework net10.0</p>
                  <p className="text-slate-500 mt-2"># 2. 生成版本化迁移脚本 (生产环境必须经过评审):</p>
                  <p>aspros db migration script --project ./Xr.Trade.Infrastructure --context TradeDbContext --idempotent</p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 8: 全景架构审查与演进 */}
        {activeTab === 'audit' && <FrameworkAuditDashboard />}
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800/80 bg-slate-900/40 py-4 px-6 text-center text-xs text-slate-500 flex flex-col sm:flex-row items-center justify-between max-w-7xl mx-auto w-full">
        <span>Aspros Basic Framework • .NET 10 (net10.0 LTS) Microservice Platform</span>
        <span className="font-mono mt-1 sm:mt-0">Node.js 22 Runtime • Express 4 + Vite & React</span>
      </footer>
    </div>
  );
}
