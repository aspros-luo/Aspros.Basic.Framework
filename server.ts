import express from 'express';
import cors from 'cors';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = 3000;

app.use(cors());
app.use(express.json());

// ==========================================
// 1. Multi-Tenant Microservice Seed Data
// ==========================================

export interface OrderItem {
  sku: string;
  title: string;
  unitPrice: number;
  quantity: number;
}

export interface OrderAggregate {
  id: string;
  tenantId: number;
  customerName: string;
  items: OrderItem[];
  totalAmount: number;
  currency: string;
  status: 'Draft' | 'Paid' | 'Processing' | 'Shipped' | 'Cancelled';
  deleted: boolean;
  createdTime: string;
  updatedTime: string;
  idempotencyKey?: string;
}

export interface InventoryEntity {
  sku: string;
  name: string;
  tenantId: number;
  availableStock: number;
  lockedStock: number;
  safetyThreshold: number;
  updatedTime: string;
}

export interface PaymentEntity {
  paymentId: string;
  orderId: string;
  tenantId: number;
  amount: number;
  currency: string;
  channel: 'Alipay' | 'WeChatPay' | 'UnionPay' | 'CorporateTransfer';
  status: 'Success' | 'Pending' | 'Failed';
  transactionNo: string;
  createdTime: string;
}

export interface StagedChange {
  id: string;
  type: 'Add' | 'Update' | 'SoftDelete' | 'HardDelete';
  entity: Record<string, any>;
  timestamp: string;
}

export interface ServiceInstance {
  id: string;
  serviceName: string;
  groupName: string;
  ip: string;
  port: number;
  healthy: boolean;
  weight: number;
  metadata: Record<string, string>;
  lastHeartbeat: string;
}

export interface CapOutboxMessage {
  id: string;
  topic: string;
  group: string;
  content: Record<string, any>;
  status: 'Pending' | 'Sent' | 'Consumed' | 'Failed';
  retryCount: number;
  createdTime: string;
  consumedTime?: string;
  transactionId?: string;
  contractType?: 'IIntegrationEvent' | 'ILocalDomainEvent';
}

// Initial Inventories
let inventoryDb: InventoryEntity[] = [
  {
    sku: 'SKU-PRO-SERVER-16C',
    name: '企业级云主机节点 (16核/64G)',
    tenantId: 100,
    availableStock: 35,
    lockedStock: 4,
    safetyThreshold: 10,
    updatedTime: new Date(Date.now() - 3600000).toISOString(),
  },
  {
    sku: 'SKU-GPU-ACCEL-A100',
    name: '分布式训练算力卡单元 (80GB显存)',
    tenantId: 100,
    availableStock: 8,
    lockedStock: 2,
    safetyThreshold: 3,
    updatedTime: new Date(Date.now() - 1800000).toISOString(),
  },
  {
    sku: 'SKU-IOT-GATEWAY-V2',
    name: '边缘计算微服务工业网关',
    tenantId: 200,
    availableStock: 120,
    lockedStock: 15,
    safetyThreshold: 20,
    updatedTime: new Date(Date.now() - 7200000).toISOString(),
  },
  {
    sku: 'SKU-NVME-STORAGE-4TB',
    name: '高速持久化分布式存储阵列',
    tenantId: 200,
    availableStock: 45,
    lockedStock: 5,
    safetyThreshold: 10,
    updatedTime: new Date(Date.now() - 5400000).toISOString(),
  },
];

// Initial Orders
let orderDb: OrderAggregate[] = [
  {
    id: 'ORD-20261018-001',
    tenantId: 100,
    customerName: '华东数智科技有限公司',
    items: [{ sku: 'SKU-PRO-SERVER-16C', title: '企业级云主机节点 (16核/64G)', unitPrice: 4800, quantity: 2 }],
    totalAmount: 9600,
    currency: 'CNY',
    status: 'Paid',
    deleted: false,
    createdTime: new Date(Date.now() - 7200000).toISOString(),
    updatedTime: new Date(Date.now() - 7000000).toISOString(),
  },
  {
    id: 'ORD-20261018-002',
    tenantId: 100,
    customerName: '上海智联高科实验室',
    items: [{ sku: 'SKU-GPU-ACCEL-A100', title: '分布式训练算力卡单元 (80GB显存)', unitPrice: 28500, quantity: 1 }],
    totalAmount: 28500,
    currency: 'CNY',
    status: 'Processing',
    deleted: false,
    createdTime: new Date(Date.now() - 3600000).toISOString(),
    updatedTime: new Date(Date.now() - 3400000).toISOString(),
  },
  {
    id: 'ORD-20261018-003',
    tenantId: 200,
    customerName: '北京北极星物联网络',
    items: [
      { sku: 'SKU-IOT-GATEWAY-V2', title: '边缘计算微服务工业网关', unitPrice: 1650, quantity: 10 },
      { sku: 'SKU-NVME-STORAGE-4TB', title: '高速持久化分布式存储阵列', unitPrice: 3200, quantity: 2 },
    ],
    totalAmount: 22900,
    currency: 'CNY',
    status: 'Shipped',
    deleted: false,
    createdTime: new Date(Date.now() - 10800000).toISOString(),
    updatedTime: new Date(Date.now() - 9000000).toISOString(),
  },
  {
    id: 'ORD-20261018-004',
    tenantId: 200,
    customerName: '已作废测试商户',
    items: [{ sku: 'SKU-IOT-GATEWAY-V2', title: '边缘计算微服务工业网关', unitPrice: 1650, quantity: 1 }],
    totalAmount: 1650,
    currency: 'CNY',
    status: 'Cancelled',
    deleted: true,
    createdTime: new Date(Date.now() - 14400000).toISOString(),
    updatedTime: new Date(Date.now() - 12000000).toISOString(),
  },
];

// Initial Payments
let paymentDb: PaymentEntity[] = [
  {
    paymentId: 'PAY-20261018-01',
    orderId: 'ORD-20261018-001',
    tenantId: 100,
    amount: 9600,
    currency: 'CNY',
    channel: 'CorporateTransfer',
    status: 'Success',
    transactionNo: 'TXN-ABC-992100',
    createdTime: new Date(Date.now() - 7000000).toISOString(),
  },
  {
    paymentId: 'PAY-20261018-02',
    orderId: 'ORD-20261018-002',
    tenantId: 100,
    amount: 28500,
    currency: 'CNY',
    channel: 'UnionPay',
    status: 'Success',
    transactionNo: 'TXN-ABC-992101',
    createdTime: new Date(Date.now() - 3400000).toISOString(),
  },
];

// Staged changes for UnitOfWork
let stagedUowChanges: StagedChange[] = [];

// Service Discovery Instances (Nacos multi-node)
let serviceInstances: ServiceInstance[] = [
  {
    id: 'xr-trade-node-01',
    serviceName: 'xr.trade',
    groupName: 'DEFAULT_GROUP',
    ip: '10.0.1.20',
    port: 8080,
    healthy: true,
    weight: 100,
    metadata: { version: '1.2.0', zone: 'zone-east-1a', cluster: 'k8s-prod' },
    lastHeartbeat: new Date().toISOString(),
  },
  {
    id: 'xr-trade-node-02',
    serviceName: 'xr.trade',
    groupName: 'DEFAULT_GROUP',
    ip: '10.0.1.21',
    port: 8080,
    healthy: true,
    weight: 80,
    metadata: { version: '1.2.0', zone: 'zone-east-1b', cluster: 'k8s-prod' },
    lastHeartbeat: new Date().toISOString(),
  },
  {
    id: 'grpc-provider-01',
    serviceName: 'framework-core-provider',
    groupName: 'DEFAULT_GROUP',
    ip: '10.0.2.15',
    port: 7041,
    healthy: true,
    weight: 100,
    metadata: { protocol: 'gRPC/HTTP2', runtime: '.NET 10 (net10.0 LTS)' },
    lastHeartbeat: new Date().toISOString(),
  },
  {
    id: 'saas-system-auth',
    serviceName: 'saas-system',
    groupName: 'DEFAULT_GROUP',
    ip: '10.0.3.10',
    port: 9000,
    healthy: true,
    weight: 100,
    metadata: { role: 'auth-rbac', validationPath: '/system/user.permission.valid' },
    lastHeartbeat: new Date().toISOString(),
  },
  {
    id: 'payment-service-node',
    serviceName: 'payment.service',
    groupName: 'DEFAULT_GROUP',
    ip: '10.0.4.5',
    port: 8085,
    healthy: true,
    weight: 100,
    metadata: { version: '2.0.1', pciDssCompliant: 'true' },
    lastHeartbeat: new Date().toISOString(),
  },
];

// CAP Outbox Message Store
let capOutbox: CapOutboxMessage[] = [
  {
    id: 'cap-msg-101',
    topic: 'aspros.order.created',
    group: 'inventory-consumer',
    content: { orderId: 'ORD-20261018-001', tenantId: 100, totalAmount: 9600, sku: 'SKU-PRO-SERVER-16C', quantity: 2 },
    status: 'Consumed',
    retryCount: 0,
    createdTime: new Date(Date.now() - 7200000).toISOString(),
    consumedTime: new Date(Date.now() - 7198000).toISOString(),
    transactionId: 'tx-2026-001',
  },
  {
    id: 'cap-msg-102',
    topic: 'aspros.payment.success',
    group: 'notification-consumer',
    content: { paymentId: 'PAY-20261018-01', orderId: 'ORD-20261018-001', amount: 9600 },
    status: 'Consumed',
    retryCount: 0,
    createdTime: new Date(Date.now() - 7000000).toISOString(),
    consumedTime: new Date(Date.now() - 6998000).toISOString(),
    transactionId: 'tx-2026-002',
  },
  {
    id: 'cap-msg-103',
    topic: 'aspros.inventory.low_stock_warning',
    group: 'procurement-consumer',
    content: { sku: 'SKU-GPU-ACCEL-A100', availableStock: 8, safetyThreshold: 3 },
    status: 'Sent',
    retryCount: 1,
    createdTime: new Date(Date.now() - 1800000).toISOString(),
    transactionId: 'tx-2026-003',
  },
];

// Optimization live tracking counters
const optimizationCounters = {
  idempotencyInterceptions: 0,
  zoneAffinityAccelerations: 0,
  totalLatencySavedMs: 0,
  scopesDerived: 0,
};

// Distributed Idempotency Cache Store (60s TTL)
const idempotencyStore = new Map<string, { result: any; createdAt: number }>();

// WorkContext Scope Holder (AsyncLocal simulation)
class WorkContextScopeManager {
  private currentScope: { tenantId: number; userId: number; origin: string; scopeId: string } | null = null;

  createScope(tenantId: number, userId: number, origin = 'AsyncLocalScope') {
    this.currentScope = {
      tenantId,
      userId,
      origin,
      scopeId: `scope-${Date.now().toString(36)}`,
    };
    optimizationCounters.scopesDerived++;
    return this.currentScope;
  }

  getCurrentScope() {
    return this.currentScope;
  }

  clearScope() {
    this.currentScope = null;
  }
}

const workContextScopeManager = new WorkContextScopeManager();

// Rate Limiting tracking
const rateLimiterBuckets = new Map<string, { count: number; windowStart: number }>();

// Resilience Circuit Breaker state
let circuitBreakerState: 'CLOSED' | 'OPEN' | 'HALF_OPEN' = 'CLOSED';
let consecutiveFailures = 0;
let lastFailureTime = 0;

// ==========================================
// 2. Core Sample Endpoints (Backward Compatible)
// ==========================================

app.get('/health', (_req, res) => {
  res.json({
    service: 'framework-core-consumer',
    status: 'ok',
    timestamp: new Date().toISOString(),
  });
});

app.get('/core/provider/health', (_req, res) => {
  res.json({
    service: 'framework-core-provider',
    status: 'ok',
    timestamp: new Date().toISOString(),
  });
});

app.get('/core/ping', (req, res) => {
  const message = (req.query.message as string) ?? 'hello';
  const authHeader = req.headers.authorization;
  const replyMessage = !message || message.trim() === '' ? 'pong' : `pong: ${message}`;
  const unixTime = Math.floor(Date.now() / 1000);

  res.json({
    message: replyMessage,
    service: 'framework-core-provider',
    unixTime,
    metadata: {
      caller: 'framework-core-consumer',
      bearerTokenForwarded: authHeader ? authHeader.replace(/^Bearer\s+/i, '') : null,
      protocol: 'gRPC/HTTP2-bridged',
      latencyMs: Math.floor(Math.random() * 8) + 2,
    },
  });
});

// ==========================================
// 3. Multi-Hop Microservice Chain Simulation (with Optimizations)
// ==========================================

app.post('/core/microservice-chain', (req, res) => {
  const {
    message,
    tenantId,
    bearerToken,
    simulateTimeout,
    deadlineMs = 500,
    enableZoneAffinity = true,
    callerZone = 'zone-east-1a',
    idempotencyKey,
    useScopeContext = false,
  } = req.body;

  // 1. Idempotency Check on Chain
  if (idempotencyKey) {
    const existing = idempotencyStore.get(idempotencyKey);
    if (existing && Date.now() - existing.createdAt < 60000) {
      optimizationCounters.idempotencyInterceptions++;
      return res.json({
        ...existing.result,
        optimizations: {
          ...existing.result.optimizations,
          idempotencyApplied: true,
          idempotencyKey,
          hitCache: true,
          message: '【链路级幂等防重拦截生效】检测到重复调用，已复用既有执行结果并阻断下游连锁请求。',
        },
      });
    }
  }

  // 2. Scope Context resolution
  let effectiveTenant = tenantId ? Number(tenantId) : 100;
  if (useScopeContext) {
    const scope = workContextScopeManager.getCurrentScope() || workContextScopeManager.createScope(effectiveTenant, 88001, 'ChainScope');
    effectiveTenant = scope.tenantId;
  }

  const traceId = `trace-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;
  const correlationId = `corr-${Math.random().toString(36).slice(2, 8)}`;
  const authHeaderValue = bearerToken || 'eyJh...aspros-work-context';

  // Zone routing calculation
  const targetZone = enableZoneAffinity ? callerZone : callerZone === 'zone-east-1a' ? 'zone-east-1b' : 'zone-east-1a';
  const isSameZone = targetZone === callerZone;
  const grpcLatency = isSameZone ? 3 : 22; // 3ms same zone vs 22ms cross-zone!
  const latencySaved = isSameZone ? 19 : 0;

  if (isSameZone && enableZoneAffinity) {
    optimizationCounters.zoneAffinityAccelerations++;
    optimizationCounters.totalLatencySavedMs += latencySaved;
  }

  // Hop 1: API Gateway
  const hop1 = {
    service: 'api-gateway',
    protocol: 'HTTP/REST' as const,
    durationMs: 4,
    status: 'OK' as const,
    action: 'Ingress route validation & rate limiting check',
    zone: callerZone,
    headersPassed: {
      'x-trace-id': traceId,
      'x-tenant-id': String(effectiveTenant),
      'x-caller-zone': callerZone,
      Authorization: `Bearer ${authHeaderValue.slice(0, 15)}...`,
    },
  };

  // Hop 2: Consumer Web API
  const hop2 = {
    service: 'framework-core-consumer',
    protocol: 'HTTP/REST' as const,
    durationMs: 8,
    status: 'OK' as const,
    action: `CoreController 解析并转发 Token (Zone: ${callerZone})`,
    zone: callerZone,
    headersPassed: {
      'x-trace-id': traceId,
      'x-correlation-id': correlationId,
      'x-tenant-id': String(effectiveTenant),
      'grpc-timeout': `${deadlineMs}m`,
    },
  };

  if (simulateTimeout) {
    const hop3Timeout = {
      service: 'framework-core-provider',
      protocol: 'gRPC/HTTP2' as const,
      durationMs: deadlineMs + 50,
      status: 'TIMEOUT' as const,
      action: 'CoreGrpcService.PingAsync deadline exceeded',
      zone: targetZone,
      headersPassed: {
        'x-trace-id': traceId,
        'grpc-status': 'DEADLINE_EXCEEDED (4)',
      },
    };

    return res.status(504).json({
      traceId,
      correlationId,
      tenantId: effectiveTenant,
      overallDurationMs: hop1.durationMs + hop2.durationMs + hop3Timeout.durationMs,
      status: 'DEADLINE_EXCEEDED',
      hops: [hop1, hop2, hop3Timeout],
      finalResponse: {
        error: 'gRPC CallContext deadline exceeded. Downstream call cancelled gracefully.',
        grpcCode: 4,
      },
      optimizations: {
        zoneAffinityApplied: enableZoneAffinity,
        callerZone,
        targetZone,
        latencySavedMs: 0,
        idempotencyApplied: !!idempotencyKey,
        idempotencyKey,
        scopeContextApplied: useScopeContext,
      },
    });
  }

  // Hop 3: Trade Provider (gRPC with Zone Affinity)
  const hop3 = {
    service: 'framework-core-provider',
    protocol: 'gRPC/HTTP2' as const,
    durationMs: grpcLatency,
    status: 'OK' as const,
    action: `CoreGrpcService.PingAsync (${isSameZone ? '同机房直连 ⚡3ms' : '跨机房专线 🐢22ms'})`,
    zone: targetZone,
    headersPassed: {
      'x-trace-id': traceId,
      'x-correlation-id': correlationId,
      'x-forwarded-user': '10001',
      'x-service-zone': targetZone,
    },
  };

  // Hop 4: Downstream Payment Service
  const hop4 = {
    service: 'payment.service',
    protocol: 'Internal-IPC' as const,
    durationMs: 5,
    status: 'OK' as const,
    action: `多租户账户状态校验 (Tenant ${effectiveTenant})`,
    zone: targetZone,
    headersPassed: {
      'x-trace-id': traceId,
      'x-tenant-id': String(effectiveTenant),
    },
  };

  const totalTime = hop1.durationMs + hop2.durationMs + hop3.durationMs + hop4.durationMs;

  const result: any = {
    traceId,
    correlationId,
    tenantId: effectiveTenant,
    overallDurationMs: totalTime,
    status: 'SUCCESS',
    hops: [hop1, hop2, hop3, hop4],
    finalResponse: {
      message: `pong: ${message || 'hello'}`,
      service: 'framework-core-provider',
      tenantId: effectiveTenant,
      serverTime: new Date().toISOString(),
      grpcMetadataReceived: {
        correlationId,
        bearerTokenVerified: true,
        callContextPropagated: true,
        targetZone,
      },
    },
    optimizations: {
      zoneAffinityApplied: isSameZone && enableZoneAffinity,
      callerZone,
      targetZone,
      latencySavedMs: latencySaved,
      idempotencyApplied: !!idempotencyKey,
      idempotencyKey,
      scopeContextApplied: useScopeContext,
    },
  };

  if (idempotencyKey) {
    idempotencyStore.set(idempotencyKey, { result, createdAt: Date.now() });
  }

  res.json(result);
});

// ==========================================
// 4. Unit of Work & Complex Order-Inventory Transaction
// ==========================================

app.get('/api/framework/business/data', (req, res) => {
  const tenantId = req.query.tenantId ? Number(req.query.tenantId) : null;
  const filteredOrders = tenantId ? orderDb.filter((o) => o.tenantId === tenantId) : orderDb;
  const filteredInventories = tenantId ? inventoryDb.filter((i) => i.tenantId === tenantId) : inventoryDb;
  const filteredPayments = tenantId ? paymentDb.filter((p) => p.tenantId === tenantId) : paymentDb;

  res.json({
    success: true,
    data: {
      orders: filteredOrders,
      inventories: filteredInventories,
      payments: filteredPayments,
      stagedChanges: stagedUowChanges,
    },
  });
});

// Real-world "Create Order with Inventory Deduction" transactional scenario (with [Idempotent] & WorkContext Scope)
app.post('/api/framework/uow/order-transaction', (req, res) => {
  const { customerName, sku, quantity, tenantId, simulateFailure, useScopeContext, callerType } = req.body;
  const idempotencyKey = (req.headers['x-idempotency-key'] as string) || req.body.idempotencyKey;

  // 1. Native Idempotency Filter Check
  if (idempotencyKey) {
    const existing = idempotencyStore.get(idempotencyKey);
    if (existing && Date.now() - existing.createdAt < 60000) {
      optimizationCounters.idempotencyInterceptions++;
      return res.status(200).json({
        ...existing.result,
        hitCache: true,
        idempotencyApplied: true,
        idempotencyKey,
        message: `【[Idempotent] 切面防重拦截生效】检测到业务防重令牌 (${idempotencyKey}) 在 60 秒内重复提交。框架已自动阻断重复扣减库存并直接返回原订单计算结果。`,
      });
    }
  }

  // 2. WorkContext Scope derivation
  let targetTenant = tenantId ? Number(tenantId) : 100;
  let activeScopeInfo: any = null;
  if (useScopeContext) {
    const scope = workContextScopeManager.createScope(targetTenant, 99001, callerType || 'CapConsumer');
    targetTenant = scope.tenantId;
    activeScopeInfo = scope;
  }

  const orderQty = quantity ? Number(quantity) : 1;

  const invItem = inventoryDb.find((i) => i.sku === sku && i.tenantId === targetTenant);
  if (!invItem) {
    return res.status(404).json({ success: false, message: `未找到租户 ${targetTenant} 的商品 SKU: ${sku}` });
  }

  // 3. Stage Order Creation (RegisterNew)
  const orderId = `ORD-${Date.now().toString().slice(-6)}`;
  const unitPrice = 4800;
  const totalAmount = unitPrice * orderQty;
  const newOrder: OrderAggregate = {
    id: orderId,
    tenantId: targetTenant,
    customerName: customerName || '新增企业客户',
    items: [{ sku, title: invItem.name, unitPrice, quantity: orderQty }],
    totalAmount,
    currency: 'CNY',
    status: 'Paid',
    deleted: false,
    createdTime: new Date().toISOString(),
    updatedTime: new Date().toISOString(),
    idempotencyKey,
  };

  const stageOrder: StagedChange = {
    id: `stage-order-${orderId}`,
    type: 'Add',
    entity: newOrder,
    timestamp: new Date().toISOString(),
  };

  // 4. Stage Inventory Deduction (RegisterDirty)
  const newAvailableStock = invItem.availableStock - orderQty;
  const stageInventory: StagedChange = {
    id: `stage-inv-${sku}`,
    type: 'Update',
    entity: {
      sku: invItem.sku,
      name: invItem.name,
      tenantId: targetTenant,
      availableStock: newAvailableStock,
      lockedStock: invItem.lockedStock + orderQty,
      safetyThreshold: invItem.safetyThreshold,
      updatedTime: new Date().toISOString(),
    },
    timestamp: new Date().toISOString(),
  };

  stagedUowChanges.push(stageOrder, stageInventory);

  // If simulate failure:
  if (simulateFailure) {
    const rolledBackCount = stagedUowChanges.length;
    stagedUowChanges = []; // RollbackAsync()
    return res.status(400).json({
      success: false,
      rolledBack: true,
      message: '【事务回滚成功】模拟业务校验失败（库存锁定超限或支付超时）。已触发 UnitOfWork.RollbackAsync()，暂存操作全部撤回，数据未被破坏。',
      stagedCountCleared: rolledBackCount,
    });
  }

  // 5. Atomic Commit (CommitAsync)
  orderDb.unshift(newOrder);
  invItem.availableStock = newAvailableStock;
  invItem.lockedStock += orderQty;
  invItem.updatedTime = new Date().toISOString();

  // 6. Create CAP Outbox Message (Transactional Outbox)
  const capMsg: CapOutboxMessage = {
    id: `cap-msg-${Date.now().toString().slice(-4)}`,
    topic: 'aspros.order.created',
    group: 'inventory-and-logistics',
    content: { orderId, tenantId: targetTenant, totalAmount, sku, quantity: orderQty, idempotencyKey },
    status: 'Consumed',
    retryCount: 0,
    createdTime: new Date().toISOString(),
    consumedTime: new Date().toISOString(),
    transactionId: `tx-${Date.now().toString().slice(-6)}`,
    contractType: 'IIntegrationEvent',
  };
  capOutbox.unshift(capMsg);

  stagedUowChanges = [];

  const responseData = {
    success: true,
    message: `【原子提交成功】UnitOfWork.CommitAsync() 执行完毕。创建订单 ${orderId} 并扣减库存，同时生成 CAP 事务性发件箱消息。`,
    order: newOrder,
    inventory: invItem,
    capMessage: capMsg,
    idempotencyApplied: !!idempotencyKey,
    idempotencyKey,
    scopeContext: activeScopeInfo,
  };

  if (idempotencyKey) {
    idempotencyStore.set(idempotencyKey, { result: responseData, createdAt: Date.now() });
  }

  res.json(responseData);
});

// Generic UOW Stage endpoints
app.post('/api/framework/uow/stage-delete', (req, res) => {
  const { id } = req.body;
  const order = orderDb.find((o) => o.id === id);
  if (!order) {
    return res.status(404).json({ success: false, message: '未找到订单' });
  }

  const stageItem: StagedChange = {
    id: `del-${id}-${Date.now()}`,
    type: 'SoftDelete',
    entity: { id, customerName: order.customerName, deleted: true },
    timestamp: new Date().toISOString(),
  };

  stagedUowChanges.push(stageItem);
  res.json({ success: true, message: `已暂存逻辑软删除（RegisterDeleted）订单: ${id}`, data: stageItem });
});

app.post('/api/framework/uow/commit-staged', (_req, res) => {
  if (stagedUowChanges.length === 0) {
    return res.json({ success: true, message: '当前没有待提交的暂存改动。' });
  }

  for (const c of stagedUowChanges) {
    if (c.type === 'SoftDelete') {
      const ord = orderDb.find((o) => o.id === c.entity.id);
      if (ord) {
        ord.deleted = true;
        ord.updatedTime = new Date().toISOString();
      }
    }
  }

  const count = stagedUowChanges.length;
  stagedUowChanges = [];
  res.json({ success: true, message: `UnitOfWork 事务提交完成，共应用 ${count} 条变更。` });
});

app.post('/api/framework/uow/rollback-staged', (_req, res) => {
  const count = stagedUowChanges.length;
  stagedUowChanges = [];
  res.json({ success: true, message: `UnitOfWork.RollbackAsync() 已执行，已丢弃 ${count} 条未提交变更。` });
});

app.post('/api/framework/uow/reset-business', (_req, res) => {
  stagedUowChanges = [];
  res.json({ success: true, message: '业务用例数据已重置为初始状态。' });
});

// ==========================================
// 5. Dapper Multi-Tenant Complex Analytics
// ==========================================

app.post('/api/framework/dapper/analytics', (req, res) => {
  const { tenantId } = req.body;
  const targetTenant = tenantId ? Number(tenantId) : null;

  const validOrders = orderDb.filter((o) => !o.deleted && (targetTenant ? o.tenantId === targetTenant : true));
  const deletedOrders = orderDb.filter((o) => o.deleted && (targetTenant ? o.tenantId === targetTenant : true));

  const totalRevenue = validOrders.reduce((sum, o) => sum + o.totalAmount, 0);
  const avgOrderValue = validOrders.length > 0 ? Math.round(totalRevenue / validOrders.length) : 0;

  const tenantStats = [100, 200].map((tId) => {
    const tOrders = orderDb.filter((o) => o.tenantId === tId && !o.deleted);
    const tDeleted = orderDb.filter((o) => o.tenantId === tId && o.deleted);
    const tRevenue = tOrders.reduce((sum, o) => sum + o.totalAmount, 0);
    return {
      tenantId: tId,
      tenantName: tId === 100 ? '华东区域商户 (Tenant 100)' : '华北区域商户 (Tenant 200)',
      activeOrderCount: tOrders.length,
      softDeletedCount: tDeleted.length,
      revenueTotal: tRevenue,
      currency: 'CNY',
    };
  });

  res.json({
    success: true,
    sql: `SELECT tenant_id, COUNT(*) AS active_orders, SUM(total_amount) AS revenue, AVG(total_amount) AS avg_value
FROM OrderAggregates
WHERE is_deleted = 0
GROUP BY tenant_id`,
    metrics: {
      activeOrders: validOrders.length,
      softDeletedOrders: deletedOrders.length,
      totalRevenue,
      avgOrderValue,
      currency: 'CNY',
      executionTimeMs: 1.45,
      executor: 'IDapperExecutor (Shared DbConnection with UnitOfWork)',
    },
    tenantBreakdown: targetTenant ? tenantStats.filter((t) => t.tenantId === targetTenant) : tenantStats,
  });
});

// ==========================================
// 6. CAP Outbox & Event Messaging
// ==========================================

app.get('/api/framework/cap/messages', (_req, res) => {
  res.json({ success: true, messages: capOutbox });
});

app.post('/api/framework/cap/retry', (req, res) => {
  const { id } = req.body;
  const msg = capOutbox.find((m) => m.id === id);
  if (!msg) {
    return res.status(404).json({ success: false, message: '未找到发件箱消息' });
  }

  msg.retryCount += 1;
  msg.status = 'Consumed';
  msg.consumedTime = new Date().toISOString();

  res.json({
    success: true,
    message: `CAP 消息 [${msg.id}] 重试成功，已投递至 RabbitMQ 并被订阅端消费。`,
    messageItem: msg,
  });
});

app.post('/api/framework/cap/publish', (req, res) => {
  const { topic, group, payload } = req.body;
  const newMsg: CapOutboxMessage = {
    id: `cap-msg-${Date.now().toString().slice(-4)}`,
    topic: topic || 'aspros.business.event',
    group: group || 'default-group',
    content: payload || { emitter: 'AsprosDashboard', timestamp: new Date().toISOString() },
    status: 'Consumed',
    retryCount: 0,
    createdTime: new Date().toISOString(),
    consumedTime: new Date().toISOString(),
    transactionId: `tx-${Date.now().toString().slice(-6)}`,
  };

  capOutbox.unshift(newMsg);
  res.json({ success: true, message: `CAP 发件箱发布成功: ${newMsg.topic}`, data: newMsg });
});

// ==========================================
// 7. Nacos Service Discovery & Failover Routing
// ==========================================

app.get('/api/framework/discovery/instances', (_req, res) => {
  res.json({ success: true, instances: serviceInstances });
});

app.post('/api/framework/discovery/toggle-health', (req, res) => {
  const { id } = req.body;
  const inst = serviceInstances.find((i) => i.id === id);
  if (!inst) {
    return res.status(404).json({ success: false, message: '未找到实例' });
  }

  inst.healthy = !inst.healthy;
  inst.lastHeartbeat = new Date().toISOString();

  res.json({
    success: true,
    message: `已将实例 [${inst.id}] 健康状态切换为: ${inst.healthy ? 'HEALTHY (正常)' : 'UNHEALTHY (故障)'}`,
    instance: inst,
  });
});

app.get('/api/framework/discovery/resolve', (req, res) => {
  const serviceName = (req.query.service as string) || 'xr.trade';
  const groupName = (req.query.group as string) || 'DEFAULT_GROUP';
  const callerZone = (req.query.callerZone as string) || (req.headers['x-caller-zone'] as string) || 'zone-east-1a';
  const enableZoneAffinity = req.query.enableZoneAffinity !== 'false';

  const allMatches = serviceInstances.filter((s) => s.serviceName === serviceName && s.groupName === groupName);
  const healthyMatches = allMatches.filter((s) => s.healthy);

  if (healthyMatches.length === 0) {
    return res.status(503).json({
      success: false,
      status: 503,
      message: `【服务发现故障】服务 '${serviceName}' 在分组 '${groupName}' 中无可用健康节点！(已上线实例: ${allMatches.length}, 健康实例: 0)`,
    });
  }

  // Zone-Aware Selection
  let selected: ServiceInstance;
  let isAffinityHit = false;

  if (enableZoneAffinity && callerZone) {
    const zoneMatches = healthyMatches.filter((s) => s.metadata.zone === callerZone);
    if (zoneMatches.length > 0) {
      selected = zoneMatches[Math.floor(Math.random() * zoneMatches.length)];
      isAffinityHit = true;
      optimizationCounters.zoneAffinityAccelerations++;
      optimizationCounters.totalLatencySavedMs += 19;
    } else {
      selected = healthyMatches[Math.floor(Math.random() * healthyMatches.length)];
    }
  } else {
    selected = healthyMatches[Math.floor(Math.random() * healthyMatches.length)];
  }

  res.json({
    success: true,
    endpoint: {
      address: `http://${selected.ip}:${selected.port}/`,
      instanceId: selected.id,
      serviceName: selected.serviceName,
      groupName: selected.groupName,
      weight: selected.weight,
      zone: selected.metadata.zone || 'default-zone',
      resolvedAt: new Date().toISOString(),
      activeNodesInPool: healthyMatches.length,
      totalNodesInPool: allMatches.length,
      zoneAffinity: {
        applied: enableZoneAffinity,
        callerZone,
        targetZone: selected.metadata.zone,
        isAffinityHit,
        estimatedLatencyMs: isAffinityHit ? 3 : 22,
        latencySavedMs: isAffinityHit ? 19 : 0,
      },
    },
  });
});

// Optimization Metrics
app.get('/api/framework/metrics/optimizations', (_req, res) => {
  res.json({
    success: true,
    metrics: {
      ...optimizationCounters,
      idempotencyStoreSize: idempotencyStore.size,
      activeScope: workContextScopeManager.getCurrentScope(),
      lastUpdated: new Date().toISOString(),
    },
  });
});

// ==========================================
// 8. Permission & WorkContext Middleware Simulation
// ==========================================

app.post('/api/framework/permission/validate', (req, res) => {
  const { path, userRole, requiredPermission, tenantId = 100 } = req.body;

  if (userRole === 'anonymous') {
    return res.status(401).json({
      success: false,
      status: 401,
      message: '401 未授权: 缺失或无效的身份令牌 Bearer Token。',
    });
  }

  const rolePermissions: Record<string, string[]> = {
    admin: ['system:all', 'trade:read', 'trade:write', 'user:permission:valid', 'db:migrate', 'order:create', 'cap:retry'],
    operator: ['trade:read', 'trade:write', 'user:permission:valid', 'order:create'],
    viewer: ['trade:read', 'order:read'],
  };

  const userPerms = rolePermissions[userRole] || [];
  const needed = requiredPermission || 'trade:write';

  if (!userPerms.includes(needed) && !userPerms.includes('system:all')) {
    return res.status(403).json({
      success: false,
      status: 403,
      message: `403 权限拒绝: 用户角色 '${userRole}' 缺少权限点 '${needed}'。`,
      details: {
        validationPath: path || '/system/user.permission.valid',
        serviceName: 'saas-system',
        groupName: 'DEFAULT_GROUP',
        failClosed: true,
      },
    });
  }

  res.json({
    success: true,
    status: 200,
    message: `权限校验通过: 已授予 '${needed}' 访问许可。`,
    workContext: {
      userId: userRole === 'admin' ? 10001 : 10099,
      tenantId: Number(tenantId),
      role: userRole,
      grantedPermissions: userPerms,
    },
  });
});

// ==========================================
// 9. Resilience & Rate Limiting Simulation
// ==========================================

app.get('/api/framework/resilience/test', (req, res) => {
  const forceFailure = req.query.forceFailure === 'true';
  const now = Date.now();
  const clientIp = req.ip || '127.0.0.1';

  // Rate Limiting
  const bucket = rateLimiterBuckets.get(clientIp);
  if (!bucket || now - bucket.windowStart > 10000) {
    rateLimiterBuckets.set(clientIp, { count: 1, windowStart: now });
  } else {
    bucket.count++;
    if (bucket.count > 10) {
      return res.status(429).json({
        success: false,
        status: 429,
        error: 'Too Many Requests',
        message: 'Aspros Framework 限流策略生效: FixedWindowLimiter 超出许可配额（限制: 10次/10秒）。',
      });
    }
  }

  // Circuit Breaker State
  if (circuitBreakerState === 'OPEN') {
    if (now - lastFailureTime > 8000) {
      circuitBreakerState = 'HALF_OPEN';
    } else {
      return res.status(503).json({
        success: false,
        status: 503,
        circuitBreakerState: 'OPEN',
        message: '熔断器处于 OPEN (开启) 状态。为保护下游微服务，出站调用已被断路器快速阻断。',
        cooldownRemainingMs: Math.max(0, 8000 - (now - lastFailureTime)),
      });
    }
  }

  if (forceFailure) {
    consecutiveFailures++;
    lastFailureTime = now;
    if (consecutiveFailures >= 3) {
      circuitBreakerState = 'OPEN';
    }
    return res.status(500).json({
      success: false,
      status: 500,
      circuitBreakerState,
      consecutiveFailures,
      message: '模拟下游服务 500 故障，Polly 重试策略已记录失败次数。',
    });
  }

  if (circuitBreakerState === 'HALF_OPEN' || consecutiveFailures > 0) {
    consecutiveFailures = 0;
    circuitBreakerState = 'CLOSED';
  }

  res.json({
    success: true,
    circuitBreakerState,
    consecutiveFailures,
    message: 'HTTP 出站调用成功，应用标准弹性策略 (Timeout, Retry, Circuit Breaker)。',
    rateLimiter: {
      used: rateLimiterBuckets.get(clientIp)?.count ?? 1,
      limit: 10,
      windowMs: 10000,
    },
  });
});

// Optimization metrics endpoint for console header live dashboard
app.get('/api/framework/optimizations/metrics', (_req, res) => {
  res.json({
    success: true,
    metrics: {
      idempotencyInterceptions: optimizationCounters.idempotencyInterceptions,
      zoneAffinityAccelerations: optimizationCounters.zoneAffinityAccelerations,
      totalLatencySavedMs: optimizationCounters.totalLatencySavedMs,
      scopesDerived: optimizationCounters.scopesDerived,
      activeCachedIdempotencyKeys: idempotencyStore.size,
      currentScope: workContextScopeManager.getCurrentScope(),
    },
  });
});

// ==========================================
// 10. Framework Architecture Audit & Sandbox APIs
// ==========================================

app.get('/api/framework/audit/report', (_req, res) => {
  res.json({
    success: true,
    generatedAt: new Date().toISOString(),
    frameworkVersion: 'Aspros.Basic.Framework 2.0.0 (.NET 10 Native LTS)',
    targetFramework: 'net10.0',
    dimensions: [
      {
        id: 'ddd',
        name: '领域分层与解耦 (DDD Layering)',
        score: 96,
        grade: 'A+',
        status: 'EXCELLENT',
        summary: '严格遵守 Abstractions <- Domain <- Infrastructure 单向依赖，.NET 10 领域模型与 Native AOT 纯净。',
        strengths: [
          'Domain 零依赖 Infrastructure 与第三方 ORM，保持极高纯净度与 Native AOT 零裁剪支持',
          '实体采用统一 BaseEntity / TenantEntity 规范，结合 C# 14 现代属性语法约束',
          '通过 AddAutoInject() 配合 Source Generator 编译期扫描，无反射启动加速',
        ],
        weaknesses: [
          'WorkContext 强绑定 IHttpContextAccessor，需采用 .NET 10 统一 AsyncLocal Scope 模式',
          '进程内 IEventBus 需在契约层与 CAP 分布式消息形成编译期屏障',
        ],
      },
      {
        id: 'transactions',
        name: '事务与持久化架构 (UnitOfWork & Dapper)',
        score: 95,
        grade: 'A',
        status: 'EXCELLENT',
        summary: '彻底杜绝隐式自动提交，EF Core 与 Dapper 共享 .NET 10 物理 DbTransaction 事务。',
        strengths: [
          'UnitOfWork 绝不在 RegisterNew/Dirty 隐式 SaveChanges，事务控制权属于业务层',
          'IDapperExecutor 无缝复用当前 EF Core 开启的底层 DbTransaction，无需两阶段提交',
          '通过 BeginTransaction() 显式开启跨表原子事务，支持 System.Threading.Lock 保护状态机',
        ],
        weaknesses: [
          '大批量数据写入（BulkInsert）可进一步采用 .NET 10 Memory<T> 与 ReadOnlySpan<T> 零拷贝批处理',
          '软删除拦截器需配合 EF Core 编译期查询过滤器与 Dapper 统一视图',
        ],
      },
      {
        id: 'reliability',
        name: '微服务高可用与容灾 (Resilience & Discovery)',
        score: 93,
        grade: 'A',
        status: 'EXCELLENT',
        summary: '引入 .NET 10 原生 HybridCache、FrozenDictionary 路由表与 Microsoft.Extensions.Resilience。',
        strengths: [
          '统一 gRPC 客户端托管与 ForwardAuthorizationHeader 令牌自动透传',
          'PropagateGrpcCallContext 自动级联传递 Deadline 与 CancellationToken',
          '采用 .NET 10 原生 HybridCache 替代传统分布式缓存，提供内置 Stampede 防击穿保护',
        ],
        weaknesses: [
          '服务发现需进一步固化多可用区就近路由（Zone-Affinity）与动态权重衰减',
          '声明式分布式幂等防重切面需结合 .NET 10 System.Threading.Lock 与 Redis 租约锁',
        ],
      },
      {
        id: 'operations',
        name: '生产运维与工程准备度 (Ops & Observability)',
        score: 92,
        grade: 'A-',
        status: 'EXCELLENT',
        summary: '全面支持 .NET 10 Native AOT 编译、W3C TraceContext 与 OpenTelemetry 10.0。',
        strengths: [
          '提供专属命令行工具 Aspros.Basic.Framework.Tools，生成幂等性 SQL 迁移脚本',
          '就绪探针 (Readiness) 与存活探针 (Liveness) 严格分离，适配 Kubernetes',
          '全面启用 .NET 10 Native AOT 编译配置，容器冷启动时间缩减至 8ms 内',
        ],
        weaknesses: [
          '分布式链路追踪需全面统一注入 W3C TraceState 与 ActivitySource',
          '网关与内部 RPC 异常向 RFC 7807 (ProblemDetails) 的转换契约需自动化',
        ],
      },
    ],
    findings: [
      {
        id: 'FINDING-001',
        title: 'WorkContext 强耦合 IHttpContextAccessor 导致非 Web 场景上下文丢失',
        severity: 'HIGH',
        dimension: 'ddd',
        currentStatus: 'WorkContext 仅从 IHttpContextAccessor.HttpContext?.User 中解析 UserId 和 TenantId。',
        productionRisk: '当在 CAP 消息消费者、Quartz/Hangfire 后台调度、或 gRPC 独立无 HTTP 请求的后台线程中使用时，获取到的 TenantId 和 UserId 均为 0，导致多租户数据穿透或写入错误租户。',
        recommendation: '面向 .NET 10 引入 IWorkContextAccessor 与 IWorkContextScopeFactory，基于 AsyncLocal 与 C# 14 primary constructor 构建轻量无锁隔离上下文。',
        codeSnippet: `// 推荐改进：面向 .NET 10 (C# 14) 的 WorkContext Scope 模式
public sealed class WorkContext(
    IHttpContextAccessor? httpAccessor = null,
    IWorkContextScopeHolder? scopeHolder = null,
    TimeProvider? timeProvider = null) : IWorkContext
{
    // C# 14 简化属性与 System.Threading.Lock 并发守护
    private readonly System.Threading.Lock _contextLock = new();

    public Task<long> GetTenantIdAsync(CancellationToken ct = default)
    {
        // 1. 优先使用当前异步上下文派生的 Scope (.NET 10 AsyncLocal 驱动)
        if (scopeHolder?.CurrentScope is { } scope)
            return Task.FromResult(scope.TenantId);

        // 2. 次选从 HTTP Claims 中快速解析
        var claim = httpAccessor?.HttpContext?.User.FindFirst("tenant_id");
        return Task.FromResult(claim is not null && long.TryParse(claim.Value, out var id) ? id : 0L);
    }
}`,
      },
      {
        id: 'FINDING-002',
        title: '缺少开箱即用的声明式幂等防重控制 (Idempotency Filter)',
        severity: 'HIGH',
        dimension: 'reliability',
        currentStatus: '文档虽然给出了幂等性与分布式锁的指导原则，但框架代码中未提供统一的拦截过滤器或特性。',
        productionRisk: '在网络抖动或前端重复点击时，支付扣款、下单或库存扣减极易发生重复提交，破坏业务一致性。',
        recommendation: '利用 .NET 10 原生 HybridCache（L1内存+L2分布式两级缓存带防击穿保护）与 [Idempotent] 特性切面构建毫秒级防重屏障。',
        codeSnippet: `// 推荐改进：面向 .NET 10 原生 HybridCache 的声明式幂等切面
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute(int expirySeconds = 60) : Attribute, IAsyncActionFilter
{
    public string KeyTemplate { get; init; } = "{tenantId}:{orderId}";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // .NET 10 原生两级混合缓存 (L1 In-Memory + L2 Distributed Redis)
        var cache = context.HttpContext.RequestServices.GetRequiredService<HybridCache>();
        var lockKey = BuildKey(context);

        var isNewExecution = await cache.GetOrCreateAsync(
            lockKey,
            async cancel => true,
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(expirySeconds) });

        if (!isNewExecution)
        {
            context.Result = new ConflictObjectResult(ResultModel.Fail("检测到重复请求，幂等防护拦截已生效"));
            return;
        }

        await next();
    }
}`,
      },
      {
        id: 'FINDING-003',
        title: 'Nacos 服务发现缺乏多可用区就近亲和性路由 (Zone-Affinity)',
        severity: 'MEDIUM',
        dimension: 'reliability',
        currentStatus: 'NacosServiceDiscovery 仅通过 Nacos SDK 的 GetHealthyEndpointAsync 进行随机/轮询解析。',
        productionRisk: '在多机房或多可用区 (Multi-Zone/K8s Cluster) 部署时，请求经常跨机房往返调用，增加数十毫秒的网络延时并产生跨区流量费用。',
        recommendation: '采用 .NET 10 FrozenDictionary 与 ReadOnlySpan<char> 零分配快速查表，优先直连同可用区（Zone）微服务实例。',
        codeSnippet: `// 推荐改进：面向 .NET 10 的 FrozenDictionary 高性能就近路由解析
public async Task<ServiceEndpoint?> GetZoneAwareEndpointAsync(
    string serviceName, string groupName = "DEFAULT_GROUP", CancellationToken ct = default)
{
    var instances = await _namingService.SelectInstances(serviceName, groupName, healthy: true);
    var localZone = Environment.GetEnvironmentVariable("POD_ZONE") ?? "zone-east-1a";

    // .NET 10 FrozenSet / FrozenDictionary 零 GC 快速分组检索
    var zoneMap = instances.ToLookup(i => i.Metadata.GetValueOrDefault("zone") ?? "default");

    var target = zoneMap[localZone].FirstOrDefault() ?? instances.FirstOrDefault();
    return target is not null ? new ServiceEndpoint(target.Ip, target.Port) : null;
}`,
      },
      {
        id: 'FINDING-004',
        title: '进程内 IEventBus 易被误当分布式 MQ 使用，缺乏运行时安全警示',
        severity: 'MEDIUM',
        dimension: 'ddd',
        currentStatus: 'IEventBus.PublishAsync 与 AddFrameworkCap 并存，两者没有类型体系上的隔离。',
        productionRisk: '研发人员可能在跨微服务的业务场景中误调用 IEventBus，进程重启时消息丢失，导致分布式数据状态永久不一致。',
        recommendation: '将事件契约分为 ILocalDomainEvent 与 IIntegrationEvent，使用 .NET 10 readonly record struct 保证零分配与类型级编译期隔离。',
        codeSnippet: `// 推荐改进：面向 .NET 10 的事件契约类型级硬隔离
public interface ILocalDomainEvent : IEvent { }       // 进程内观察者，严禁跨进程 (内存分发)

// 跨微服务分布式最终一致性：强制通过 CAP Outbox 原子持久化投递
public interface IIntegrationEvent : ICapSubscribe
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}`,
      },
    ],
  });
});

// .NET 10 Runtime Specification API
app.get('/api/framework/system/dotnet10-spec', (_req, res) => {
  res.json({
    success: true,
    targetFramework: 'net10.0',
    runtimeVersion: '.NET 10.0.100-rtm (CoreCLR 10.0.0)',
    languageVersion: 'C# 14',
    channel: 'LTS (Long Term Support)',
    aotSupport: 'Native AOT Ready (Zero JIT, PublishAot=true)',
    features: [
      {
        name: 'System.Threading.Lock',
        category: 'Runtime',
        beforeDotNet10: '传统基于 object 的 Monitor.Enter/Exit 锁，存在隐式装箱及额外堆开销',
        inDotNet10: '专用原生 Lock 结构，lock(myLock) 编译为 Lock.EnterScope()，纯纳秒级无装箱并发锁',
        benefit: '高并发状态机与幂等流水线无垃圾回收扰动',
        benchmarkImpact: '锁竞争延迟降低 42%，分配内存降至 0 字节',
        codeSnippet: 'private readonly System.Threading.Lock _stateLock = new();\nusing (_stateLock.EnterScope()) { /* 零开销并发临界区 */ }',
      },
      {
        name: 'HybridCache 两级混合缓存',
        category: 'Caching',
        beforeDotNet10: 'IDistributedCache 与 IMemoryCache 分离，需手写双层封装并容易引发缓存击穿/雪崩',
        inDotNet10: '官方原生两级缓存抽象，自动协调 L1 (进程内内存) 与 L2 (Redis 分布式)，内置防击穿 (Stampede)',
        benefit: '单键高并发并发穿透保护，减少 95% Redis 跨网络读取',
        benchmarkImpact: '热点数据读取吞吐提升 6.8x，P99 延时稳定在 0.4ms 内',
        codeSnippet: 'var order = await hybridCache.GetOrCreateAsync("order:1001", async ct => await db.LoadOrderAsync(ct));',
      },
      {
        name: 'FrozenDictionary 冻结只读路由表',
        category: 'Performance',
        beforeDotNet10: '运行时 ConcurrentDictionary / Dictionary 需维护哈希桶扩展，存在哈希碰撞与锁开销',
        inDotNet10: '初始化期冻结并生成针对键集定制的完美哈希表 (Perfect Hash Tables)，彻底消除查找碰撞',
        benefit: '服务发现与多机房可用区 (Zone-Affinity) 路由秒级命中',
        benchmarkImpact: '路由查找吞吐提升 3.4x，查找耗时 < 5ns',
        codeSnippet: 'private static readonly FrozenDictionary<string, Endpoint> RouteTable = BuildRoutes().ToFrozenDictionary();',
      },
      {
        name: 'ClickHouse SIMD Vector512 列存加速',
        category: 'Performance',
        beforeDotNet10: 'LINQ 或标量循环遍历大数组，受限于 CPU 标量指令流水线',
        inDotNet10: '深度集成 System.Runtime.Intrinsics.Vector512<T>，单指令周期并行处理 16 个 32位整型日志列',
        benefit: '海量遥测日志与用户行为特征向量秒级多维聚合',
        benchmarkImpact: 'ClickHouse 列存扫描速率突破 1.85M 行/秒，CPU 周期降低 75%',
        codeSnippet: 'var vecSum = Vector512.Add(vecA, vecB); // SIMD 512 位列存向量聚合',
      },
      {
        name: 'Native AOT 容器原生零 JIT 编译',
        category: 'CloudNative',
        beforeDotNet10: 'JIT 编译导致微服务冷启动慢 (2~5s)，包含完整 CLR 运行时，容器镜像 > 200MB',
        inDotNet10: '提前编译为平台原生机器码 (PublishAot=true)，移除 JIT 编译器与反射代码，镜像 < 30MB',
        benefit: 'Kubernetes Serverless 极速扩缩容，秒级拉起微服务 Pod',
        benchmarkImpact: '微服务启动时间从 2800ms 锐减至 7.5ms，内存底噪降低 72%',
        codeSnippet: '<PublishAot>true</PublishAot>\n<OptimizationPreference>Speed</OptimizationPreference>',
      },
    ],
    upgradedModules: [
      {
        moduleName: 'Aspros.Base.Framework.Abstractions',
        assembly: 'Aspros.Base.Framework.Abstractions.dll',
        net10Optimization: '基于 C# 14 原生 record struct 与 params ReadOnlySpan<T> 零分配抽象',
        status: 'ACTIVE_NET10',
      },
      {
        moduleName: 'Aspros.Base.Framework.Domain',
        assembly: 'Aspros.Base.Framework.Domain.dll',
        net10Optimization: '纯净领域模型，采用 System.Threading.Lock 守卫状态机，Native AOT 零裁剪支持',
        status: 'ACTIVE_NET10',
      },
      {
        moduleName: 'Aspros.Base.Framework.Infrastructure',
        assembly: 'Aspros.Base.Framework.Infrastructure.dll',
        net10Optimization: '内置 HybridCache 混合两级缓存、FrozenDictionary 服务发现与 ClickHouse SIMD 驱动',
        status: 'ACTIVE_NET10',
      },
      {
        moduleName: 'Aspros.Basic.Framework.Tools',
        assembly: 'dotnet-aspros-tools',
        net10Optimization: '.NET 10 全局 CLI，生成严格幂等 SQL 数据库迁移与 AOT 兼容性检查报告',
        status: 'ACTIVE_NET10',
      },
    ],
  });
});

// Sandbox 1: Background Worker / MQ Consumer Scoped WorkContext Simulation
app.post('/api/framework/audit/sandbox/scoped-context', (req, res) => {
  const { callerType = 'Worker', tenantId = 200, userId = 88001 } = req.body;

  // Simulate HttpContextAccessor returning null
  const httpContextAvailable = false;

  // Derived AsyncLocal scope
  const derivedScope = {
    scopeId: `scope-${Date.now().toString(36)}`,
    caller: callerType,
    tenantId: Number(tenantId),
    userId: Number(userId),
    createdAt: new Date().toISOString(),
    traceId: `worker-trace-${Math.random().toString(36).slice(2, 8)}`,
  };

  res.json({
    success: true,
    message: `【WorkContext Scope 派生成功】已在非 HTTP 场景（${callerType}）中通过 AsyncLocal 安全承载租户与身份上下文。`,
    simulation: {
      httpContextFound: httpContextAvailable,
      fallbackUsed: 'IWorkContextScopeHolder.CreateScope()',
      activeContext: derivedScope,
      dataFilterApplied: `WHERE tenant_id = ${derivedScope.tenantId}`,
    },
  });
});

// Sandbox 2: Distributed Idempotency Key Validation
app.post('/api/framework/audit/sandbox/idempotency', (req, res) => {
  const { idempotencyKey, orderId = 'ORD-20261018-099', amount = 1200 } = req.body;
  if (!idempotencyKey) {
    return res.status(400).json({ success: false, message: '必须提供幂等防重 Key (Idempotency Key)' });
  }

  const now = Date.now();
  const existing = idempotencyStore.get(idempotencyKey);

  if (existing && now - existing.createdAt < 60000) {
    return res.json({
      success: true,
      hitCache: true,
      status: 200,
      message: '【幂等拦截生效】检测到 60 秒内重复提交相同的业务请求。框架已跳过重复执行，直接返回既有计算结果。',
      idempotencyKey,
      cachedTime: new Date(existing.createdAt).toLocaleTimeString(),
      originalResult: existing.result,
    });
  }

  // First execution: process and store
  const executionResult = {
    orderId,
    amount,
    currency: 'CNY',
    processedAt: new Date().toISOString(),
    transactionStatus: 'DEDUCTED_AND_COMMITTED',
  };

  idempotencyStore.set(idempotencyKey, { result: executionResult, createdAt: now });

  res.json({
    success: true,
    hitCache: false,
    status: 201,
    message: '【首次请求】幂等锁锁定成功，已执行业务并对防重 Token 施加 60 秒有效期。',
    idempotencyKey,
    executionResult,
  });
});

// Sandbox 3: Zone-Aware Routing Simulation
app.post('/api/framework/audit/sandbox/zone-routing', (req, res) => {
  const { callerZone = 'zone-east-1a' } = req.body;

  const tradeInstances = serviceInstances.filter((s) => s.serviceName === 'xr.trade');
  const healthyInstances = tradeInstances.filter((s) => s.healthy);

  if (healthyInstances.length === 0) {
    return res.status(503).json({ success: false, message: '无健康实例' });
  }

  // Zone match
  const zoneMatches = healthyInstances.filter((s) => (s.metadata.zone || '') === callerZone);
  const selected = zoneMatches.length > 0 ? zoneMatches[0] : healthyInstances[0];
  const isAffinityHit = (selected.metadata.zone || '') === callerZone;

  res.json({
    success: true,
    callerZone,
    isAffinityHit,
    message: isAffinityHit
      ? `【就近路由命中】调用方位于 ${callerZone}，直接命中同可用区实例 ${selected.id}，节省跨机房网络延迟约 15~25ms。`
      : `【降级跨机房路由】同可用区 (${callerZone}) 无健康节点，已安全降级路由至备用机房实例 ${selected.id}。`,
    selectedEndpoint: {
      id: selected.id,
      address: `http://${selected.ip}:${selected.port}/`,
      zone: selected.metadata.zone,
      weight: selected.weight,
    },
    candidatesPool: tradeInstances.map((t) => ({
      id: t.id,
      zone: t.metadata.zone,
      healthy: t.healthy,
      isSameZone: t.metadata.zone === callerZone,
    })),
  });
});

// Sandbox 4: Dual-Track Event Contract Isolation Guardrail Simulation
app.post('/api/framework/audit/sandbox/event-guardrail', (req, res) => {
  const { contractType = 'IIntegrationEvent', topic = 'aspros.order.dispatched', payload = {} } = req.body;

  if (contractType === 'ILocalDomainEvent') {
    return res.json({
      success: true,
      violation: true,
      guardrailTriggered: true,
      message: '【架构护栏拦截】检测到尝试将进程内事件 (ILocalDomainEvent) 发布至分布式消息总线！',
      details: 'ILocalDomainEvent 仅设计用于同一进程内单体/微服务本地内存发布与订阅。若容器重启或发生异常，内存事件直接丢失且不可重试，将导致分布式数据永久不一致。',
      remedy: '按 Aspros 架构规范，将事件类型修改为 IIntegrationEvent，并通过 CAP Transactional Outbox 在本地原子事务中持久化落库。',
      topic,
      contractType,
    });
  }

  res.json({
    success: true,
    violation: false,
    guardrailTriggered: false,
    message: '【契约合规通过】IIntegrationEvent 遵循跨微服务最终一致性生产标准。',
    details: '事件与本地实体更新在同一个数据库事务 (CapTransaction) 中原子写入发件箱，支持 MQ 异步投递与指数退避重试。',
    contractType,
    topic,
    persistedMessageId: `CAP-${Date.now().toString(36).toUpperCase()}`,
  });
});

// ==========================================
// 10. Formal Verification & Chaos Harness Endpoints
// ==========================================
import { verificationEngine } from './src/core/verificationEngine';

app.post('/api/framework/verification/run-chaos-suite', async (req, res) => {
  try {
    const profile = req.body;
    const receipt = await verificationEngine.runChaosVerification(profile);
    res.json({ success: true, receipt });
  } catch (err: any) {
    console.error('Chaos Verification Error:', err);
    res.status(500).json({ success: false, error: err.message });
  }
});

app.get('/api/framework/verification/receipts', (_req, res) => {
  res.json({ success: true, receipts: verificationEngine.getReceipts() });
});

app.get('/api/framework/verification/idempotency-state', (_req, res) => {
  res.json({
    success: true,
    records: verificationEngine.getIdempotencyRecords(),
    stockSnapshot: verificationEngine.getStockSnapshot(),
  });
});

app.post('/api/framework/verification/reset', (_req, res) => {
  verificationEngine.reset();
  res.json({
    success: true,
    message: '形式化验证基线与状态机已完全重置为初始零态。',
    stockSnapshot: verificationEngine.getStockSnapshot(),
  });
});

app.get('/api/framework/verification/tla-spec', (req, res) => {
  const protocol = (req.query.protocol as any) || '2PC_IDEMPOTENT';
  const spec = verificationEngine.generateTlaSpecification(protocol);
  res.json({ success: true, protocol, tlaSpec: spec });
});

// ==========================================
// 11. ClickHouse (CK) Columnar Analytics & "千人千面" Endpoints
// ==========================================
import { clickhouseEngine } from './src/core/clickhouseEngine';

// Get persona catalog
app.get('/api/framework/ck/personas', (_req, res) => {
  try {
    res.json({
      success: true,
      personas: clickhouseEngine.listPersonas(),
      activeUserId: clickhouseEngine.getActiveUserId(),
    });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// Get personalized profile and dynamic recommendation ("千人千面")
app.get('/api/framework/ck/persona', (req, res) => {
  try {
    const userId = req.query.userId as string | undefined;
    const profile = clickhouseEngine.getPersonaProfile(userId);
    res.json({ success: true, profile });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// Switch active persona
app.post('/api/framework/ck/persona/switch', (req, res) => {
  try {
    const { userId } = req.body;
    if (!userId) {
      return res.status(400).json({ success: false, error: '缺少 userId 参数' });
    }
    clickhouseEngine.setActiveUser(userId);
    const profile = clickhouseEngine.getPersonaProfile(userId);
    res.json({
      success: true,
      message: `已切换当前活跃画像为: ${profile.userName} (${profile.roleTitle})`,
      profile,
    });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// Ingest telemetry into ClickHouse columnar table
app.post('/api/framework/ck/ingest', (req, res) => {
  try {
    const record = clickhouseEngine.ingest(req.body);
    res.json({
      success: true,
      inserted: 1,
      record,
      storageStats: clickhouseEngine.getColumnarStats(),
    });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// Simulate high-density batch events for persona
app.post('/api/framework/ck/simulate-batch', (req, res) => {
  try {
    const { persona, count } = req.body;
    const result = clickhouseEngine.simulateBatchBehavior(persona || 'architect', count || 50);
    const updatedProfile = clickhouseEngine.getPersonaProfile();
    res.json({
      success: true,
      ...result,
      updatedProfile,
      storageStats: clickhouseEngine.getColumnarStats(),
    });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// ClickHouse columnar storage metrics
app.get('/api/framework/ck/metrics', (_req, res) => {
  try {
    res.json({
      success: true,
      stats: clickhouseEngine.getColumnarStats(),
    });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// ClickHouse SQL analytical query execution
app.post('/api/framework/ck/query', (req, res) => {
  try {
    const { sql } = req.body;
    if (!sql) {
      return res.status(400).json({ success: false, error: '缺少 sql 参数' });
    }
    const result = clickhouseEngine.executeQuery(sql);
    res.json({ success: true, result });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// Reset and reseed ClickHouse dataset
app.post('/api/framework/ck/reset', (_req, res) => {
  try {
    clickhouseEngine.seedBaselineColumnarData();
    res.json({
      success: true,
      message: 'ClickHouse 列存基线已重新生成 12,800+ 条高密度多维日志向量。',
      stats: clickhouseEngine.getColumnarStats(),
    });
  } catch (err: any) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// Explicit guard: Prevent unhandled /api/* requests from falling through to Vite SPA index.html
app.all('/api/*', (req, res) => {
  res.status(404).json({
    success: false,
    error: `API 路由未找到: [${req.method}] ${req.originalUrl}`,
  });
});

// ==========================================
// 11. Mount Vite Middleware for Dev / Static for Prod
// ==========================================

async function startServer() {
  const isDev = process.env.NODE_ENV !== 'production';

  if (isDev) {
    const { createServer } = await import('vite');
    const vite = await createServer({
      server: { middlewareMode: true, host: '0.0.0.0', port: PORT },
      appType: 'spa',
    });
    app.use(vite.middlewares);
  } else {
    const distPath = path.resolve(__dirname, 'dist');
    app.use(express.static(distPath));
    app.get('*', (_req, res) => {
      res.sendFile(path.resolve(distPath, 'index.html'));
    });
  }

  app.listen(PORT, '0.0.0.0', () => {
    console.log(`[Aspros Framework] Server running on http://0.0.0.0:${PORT}`);
  });
}

startServer().catch((err) => {
  console.error('[Aspros Framework] Failed to start server:', err);
  process.exit(1);
});
