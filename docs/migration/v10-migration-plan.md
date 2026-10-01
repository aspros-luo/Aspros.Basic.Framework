# Framework v10 Migration Plan

## Phase 1：基础边界

- 建立架构基线
- 补充中英文架构文档
- 引入 Kernel 基础类型
- 修正 Domain / Application / Infrastructure 依赖方向

## Phase 2：按实际需求逐步接入 Application 能力

当前不要求一次性完成完整 Application Runtime。

当前已落地：

1. 保留现有 Command / Query / Handler 抽象。
2. 保留轻量 Pipeline 机制。
3. 建立轻量 Unit of Work 事务边界。
4. 建立 Domain Event Handler / Dispatcher。
5. 建立 Integration Event Publisher 抽象。
6. 将 Domain Event → Integration Event → CAP Outbox 接入同一事务边界。

后续仍按真实业务问题驱动：

- 消息重复消费或重复请求出现后，再实现 Idempotency Behavior。
- 出现新的稳定横切问题后，再增加对应 Pipeline Behavior。
- 不为“完整架构”预先创建大量 Runtime 或基础设施抽象。

## Phase 3：基础设施演进

Infrastructure 继续复用现有技术栈，仅在实际项目中出现明确重复问题时抽象公共能力。

当前已确认并保留：

- EF Core：默认事务型持久化；
- Dapper：复杂 SQL / 专项查询；
- ClickHouse：分析、用户画像、推荐和大规模聚合；
- RPC：Application 最小调用契约，具体协议实现位于 Infrastructure；
- CAP：Integration Event / Outbox 实现。

仍不默认增加：

- RPC 注册中心抽象
- RPC 服务发现抽象
- 通用数据访问抽象
- 独立 Observability Framework
- 完整验证 Runtime

## Domain Event 与 Outbox

Domain Event 不直接依赖消息队列。

推荐链路：

```text
AggregateRoot
    ↓
Domain Event
    ↓
Domain Event Handler
    ↓
IIntegrationEventPublisher
    ↓
CAP Outbox
    ↓
Message Broker
```

涉及 Domain Event 的 Command 应使用 `IUnitOfWork.ExecuteInTransactionAsync(...)`，确保业务数据、领域事件 Handler 产生的数据库变更以及 CAP Outbox 记录位于同一个事务边界。

## Migration Strategy

Existing projects should be able to migrate gradually without requiring a full rewrite.

核心原则：**增量演进、复用现有能力、真实问题驱动抽象。**


## Application Pipeline Runtime

The lightweight Pipeline is now connected to the existing MediatR runtime.

- `ICommand` / `IQuery` are MediatR requests.
- `ICommandHandler` / `IQueryHandler` remain the Framework-facing handler contracts and adapt to MediatR through default interface implementations.
- `IPipelineBehavior` directly extends MediatR's Pipeline Behavior.
- `AutoInject()` explicitly registers discovered open generic Behaviors.

No separate Application Dispatcher or Runtime has been introduced.
