export interface FrameworkEntity {
  id: number;
  name: string;
  tenantId: number;
  status: 'Enabled' | 'Disabled';
  deleted: boolean;
  createdTime: string;
  updatedTime: string;
}

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

export interface RpcHopTrace {
  service: string;
  protocol: 'HTTP/REST' | 'gRPC/HTTP2' | 'Internal-IPC';
  durationMs: number;
  status: 'OK' | 'TIMEOUT' | 'FAILED';
  action: string;
  headersPassed: Record<string, string>;
  zone?: string;
}

export interface RpcTraceResult {
  traceId: string;
  correlationId: string;
  tenantId: number;
  overallDurationMs: number;
  status: 'SUCCESS' | 'DEADLINE_EXCEEDED' | 'CIRCUIT_TRIPPED';
  hops: RpcHopTrace[];
  finalResponse: Record<string, any>;
  optimizations?: {
    zoneAffinityApplied: boolean;
    callerZone?: string;
    targetZone?: string;
    latencySavedMs?: number;
    idempotencyApplied: boolean;
    idempotencyKey?: string;
    scopeContextApplied: boolean;
  };
}

export interface WorkContextScope {
  scopeId: string;
  origin: 'HttpContext' | 'CapConsumerScope' | 'BackgroundWorkerScope' | 'QuartzJobScope';
  tenantId: number;
  userId: number;
  createdAt: string;
  traceId: string;
}

// ==========================================
// Formal Verification & Chaos Engine Types
// ==========================================

export type DistributedProtocol = '2PC_IDEMPOTENT' | 'SAGA_COMPENSATION' | 'TCC_EXPIRATION';

export type IdempotencyState = 'Pending' | 'Executing' | 'Committed' | 'Failed';

export interface InvariantAssertionResult {
  id: string;
  name: string;
  category: 'Safety' | 'Liveness' | 'Isolation';
  mathematicalFormula: string;
  description: string;
  satisfied: boolean;
  checkedSamples: number;
  violationCount: number;
  evidence: string;
  metrics: Record<string, number | string>;
}

export interface ChaosRunProfile {
  concurrency: number;
  keySpread: 'single_hotspot' | 'clustered' | 'dispersed';
  chaosFaults: {
    latencyJitter: boolean;
    connectionDrop: boolean;
    splitBrainCrash: boolean;
    messageReordering: boolean;
  };
  jitterMinMs: number;
  jitterMaxMs: number;
  dropRatePercent: number;
  protocol: DistributedProtocol;
  leaseTimeoutMs: number;
}

export interface WorkerExecutionTrace {
  workerId: number;
  idempotencyKey: string;
  tenantId: number;
  orderSku: string;
  quantity: number;
  startTimeMs: number;
  endTimeMs: number;
  durationMs: number;
  attemptNumber: number;
  stateTransition: string;
  outcome: 'COMMITTED_FRESH' | 'IDEMPOTENT_CACHE_HIT' | 'RETRY_SUCCEEDED' | 'LOCK_SPIN_WAIT';
  physicalDbMutation: boolean;
  faultInjected?: string;
  responseHash: string;
  responsePayload: Record<string, any>;
}

export interface MachineVerifiableReceipt {
  receiptId: string;
  timestamp: string;
  protocol: DistributedProtocol;
  merkleRootHash: string;
  cryptoSignature: string;
  totalConcurrentCalls: number;
  uniqueIdempotencyKeys: number;
  physicalDbCommits: number;
  duplicateExecutionAttacksBlocked: number;
  transientConnectionRetries: number;
  averageLatencyMs: number;
  p50LatencyMs: number;
  p90LatencyMs: number;
  p99LatencyMs: number;
  invariants: InvariantAssertionResult[];
  allInvariantsPassed: boolean;
  chaosProfile: ChaosRunProfile;
  initialStockSnapshot: Record<string, { available: number; locked: number; deducted: number }>;
  finalStockSnapshot: Record<string, { available: number; locked: number; deducted: number }>;
  tracesSample: WorkerExecutionTrace[];
}

export interface IdempotencyKeyRecord {
  key: string;
  state: IdempotencyState;
  ownerWorkerId?: number;
  firstSeenTime: string;
  lastUpdatedTime: string;
  leaseExpiresAt: number;
  executionAttempts: number;
  physicalMutations: number;
  responseChecksum: string;
  tenantId: number;
  orderId?: string;
  lockedResourceSku?: string;
}

// ==========================================
// ClickHouse (CK) Columnar Analytics & Persona Types
// ==========================================

export type PersonaRole = 'architect' | 'quant_trader' | 'iot_operator' | 'security_auditor';

export interface ClickHouseEventRecord {
  eventId: string;
  timestamp: number;
  userId: string;
  userName: string;
  tenantId: number;
  personaRole: PersonaRole;
  eventType: string; // e.g. 'order_submit', 'grpc_call', 'invariance_check', 'telemetry_push'
  module: string;    // e.g. 'TradeService', 'Gateway', 'JepsenChaos', 'AuditVault'
  latencyMs: number;
  costMicroCents: number;
  payloadBytes: number;
  isError: boolean;
  metadata?: Record<string, any>;
}

export interface UserPersonaProfile {
  userId: string;
  userName: string;
  role: PersonaRole;
  roleTitle: string;
  avatarColor: string;
  tenantId: number;
  totalEvents: number;
  dominantModule: string;
  avgLatencyToleranceMs: number;
  riskToleranceTier: 'StrictZeroRisk' | 'LowLatencyHighYield' | 'MassiveThroughput' | 'AuditLedger';
  featureVector: {
    readWriteRatio: number;      // 0.0 (all write) to 1.0 (all read)
    concurrencyIntensity: number;// 0.0 to 1.0
    securityAuditFocus: number;  // 0.0 to 1.0
    throughputDemand: number;    // 0.0 to 1.0
  };
  recommendedLayout: {
    primaryWorkspace: string;
    promotedWidgets: string[];
    quickActions: Array<{
      id: string;
      title: string;
      description: string;
      badge: string;
      actionType: string;
    }>;
    customRoutingTier: {
      tierName: string;
      slaTargetLatency: string;
      cachingPolicy: string;
      consistencyLevel: string;
    };
    insightSummary: string;
  };
}

export interface ClickHouseColumnarStats {
  tableName: string;
  totalRows: number;
  columnsCount: number;
  rawMemoryBytes: number;
  compressedMemoryBytes: number;
  compressionRatio: number;
  scanRateRowsPerSec: number;
  activePartitions: number;
  lastIngestionTime: string;
}

export interface ClickHouseQueryResult {
  query: string;
  executionTimeMs: number;
  scannedRows: number;
  scannedBytes: number;
  columns: string[];
  rows: Array<Record<string, any>>;
  error?: string;
}

// ==========================================
// .NET 10 (net10.0 LTS) Runtime Specifications
// ==========================================

export interface DotNet10Feature {
  name: string;
  category: 'Language' | 'Runtime' | 'Performance' | 'CloudNative' | 'Caching';
  beforeDotNet10: string;
  inDotNet10: string;
  benefit: string;
  benchmarkImpact: string;
  codeSnippet: string;
}

export interface DotNet10RuntimeSpec {
  targetFramework: 'net10.0';
  runtimeVersion: string;
  languageVersion: 'C# 14';
  channel: 'LTS (Long Term Support)';
  aotSupport: 'Native AOT Ready (Zero JIT)';
  features: DotNet10Feature[];
  upgradedModules: Array<{
    moduleName: string;
    assembly: string;
    net10Optimization: string;
    status: 'ACTIVE_NET10';
  }>;
}

// ==========================================
// High-Concurrency & Stress Testing Types (.NET 10 CLR)
// ==========================================

export type StressScenarioType =
  | 'HIGH_FREQUENCY_TRADING'
  | 'GRPC_CASCADING_MESH'
  | 'CLICKHOUSE_COLUMNAR_BURST'
  | 'MULTI_TENANT_NOISY_NEIGHBOR'
  | 'CHAOTIC_CONCURRENCY_MIX';

export interface StressTestConfig {
  scenario: StressScenarioType;
  concurrency: number; // 10 - 500 workers
  totalRequests: number; // 100 - 5000 requests
  rampUpMode: 'instant' | 'linear' | 'spike';
  injectJitter: boolean;
  jitterMaxMs: number;
  injectDropRate: number; // 0 - 20%
  tenantContention: boolean;
  targetModule?: string;
}

export interface StressLatencyHistogram {
  lt1ms: number;
  ms1to5: number;
  ms5to10: number;
  ms10to25: number;
  ms25to50: number;
  ms50to100: number;
  gt100ms: number;
}

export interface StressStabilityAssertion {
  id: string;
  name: string;
  passed: boolean;
  formula: string;
  details: string;
  evidence: string;
}

export interface ClrRuntimeMetrics {
  activeThreads: number;
  availableThreads: number;
  lockContentionCount: number;
  lockWaitTimeAvgUs: number;
  gcGen0Collections: number;
  gcGen1Collections: number;
  gcGen2Collections: number;
  allocatedMemoryMb: number;
  zeroAllocationRate: number; // e.g. 98.4%
}

export interface TimeSeriesStressPoint {
  timeSec: number;
  currentTps: number;
  currentP99: number;
  activeWorkers: number;
  errorRate: number;
}

export interface StressMetricSnapshot {
  testId: string;
  timestamp: string;
  scenario: StressScenarioType;
  concurrency: number;
  totalRequests: number;
  successfulRequests: number;
  failedRequests: number;
  idempotentIntercepts: number;
  durationMs: number;
  tps: number;
  peakTps: number;
  latency: {
    minMs: number;
    meanMs: number;
    p50Ms: number;
    p90Ms: number;
    p99Ms: number;
    p999Ms: number;
    maxMs: number;
  };
  histogram: StressLatencyHistogram;
  clrMetrics: ClrRuntimeMetrics;
  stabilityAssertions: StressStabilityAssertion[];
  stabilityScore: number;
  grade: 'S+' | 'A' | 'B' | 'C' | 'FAIL';
  timeSeriesSamples: TimeSeriesStressPoint[];
}

// ==========================================
// AI, LLM, RAG & Autonomous Agent Types
// ==========================================

export interface RagKnowledgeChunk {
  id: string;
  title: string;
  category: 'Architecture' | 'Runtime' | 'Distributed' | 'FaultTolerance' | 'Database';
  content: string;
  tags: string[];
  relevanceScore?: number;
  wordCount?: number;
}

export interface RagRetrievalResult {
  query: string;
  topChunks: RagKnowledgeChunk[];
  augmentedPrompt: string;
  llmAnswer: string;
  retrievalTimeMs: number;
  generationTimeMs: number;
  sourceModel: string;
}

export interface AgentToolCall {
  toolName: string;
  args: Record<string, any>;
  result: Record<string, any>;
  executionTimeMs: number;
}

export interface AgentWorkflowStep {
  stepNumber: number;
  phase: 'THINK' | 'ACT' | 'OBSERVE' | 'REFLECT';
  thought: string;
  toolCall?: AgentToolCall;
  reflection?: string;
  timestamp: string;
}

export interface AgentWorkflowExecution {
  workflowId: string;
  workflowName: string;
  goal: string;
  status: 'RUNNING' | 'COMPLETED' | 'FAILED';
  steps: AgentWorkflowStep[];
  finalSynthesis: string;
  totalDurationMs: number;
  toolsUsed: string[];
  modelUsed: string;
}

export interface AiDiagnosticRequest {
  taskType: 'RCA_INCIDENT' | 'NATURAL_TO_SQL' | 'ARCHITECTURE_REVIEW' | 'CUSTOM';
  inputContext: string;
  useRag: boolean;
  temperature?: number;
}

export interface AiDiagnosticResponse {
  success: boolean;
  taskType: string;
  response: string;
  model: string;
  tokenCount?: { input: number; output: number };
  latencyMs: number;
  ragUsed: boolean;
  retrievedChunks?: RagKnowledgeChunk[];
}



