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
6. 将 Domain Event → Integration Event → CAP Outbox 作为显式消费者事务适配场景，而不是 Framework 默认行为。

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

## Legacy Event Compatibility

现有旧项目中的 `IEvent` / `IEventHandler` / `IEventBus` 等事件能力不在本次 v10 迁移中强制删除。它们作为兼容能力保留，避免一次性破坏已有业务。

新业务优先使用 v10 的 `IDomainEvent` / `IDomainEventHandler` / `IDomainEventDispatcher` 与 `IIntegrationEventPublisher`。

两套事件模型不应混用语义：旧事件总线不能被默认视为可靠的跨服务消息机制；需要跨服务可靠投递时，应使用 Integration Event + CAP Outbox + Message Broker。

## Domain Event 与 Outbox

Domain Event 不直接依赖消息队列，也不是所有业务的默认步骤。

普通业务优先使用 `IUnitOfWork.CommitAsync()`；多步骤且要求原子性的业务才使用 `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)`。



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

涉及 Domain Event 的 Command 应使用 `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)`，确保业务数据与 Domain Event Handler 产生的数据库变更位于同一个本地事务。若同时要求 CAP Outbox 记录加入该事务，由消费者 Infrastructure 提供数据库 Provider-specific CAP transaction adapter。

## v10 Source-Breaking Changes

`Aspros.Base.Framework.Domain.Status` is removed from the Domain root namespace because a generic Framework type named `Status` conflicts with business-owned `Status` types in real consumers such as Xr.Category.

The generic Framework lifecycle status is now `Aspros.Base.Framework.Domain.ValueObjects.EntityStatus` and `BasicEntity.Status` uses that type.

Consumers that use the old source form `Status = Status.Deleted` or `Status = Status.Normal` must migrate those expressions to an explicit `EntityStatus` reference. This is an intentional v10 source migration; the Framework does not reintroduce the root `Domain.Status` type merely to preserve ambiguous consumer syntax.

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

## AutoInject 定位

`AutoInject()` 只是 DI 注册便利层，不是 DDD 能力的一部分。业务项目可以完全不用它，直接使用标准 DI：`AddScoped`、`AddSingleton`、`AddTransient`、`AddMediatR`。Framework 的 Domain / Application 契约不得依赖 AutoInject 才成立。

## Package version boundary

The refactor uses **Framework v10** as the architecture/migration line and **2.0.0** as the NuGet SemVer line.

The three framework packages are aligned to:

- `Aspros.Base.Framework.Domain 2.0.0`
- `Aspros.Base.Framework.Application 2.0.0`
- `Aspros.Base.Framework.Infrastructure 2.0.0`

The 2.0.0 line is intentional because the removal of the root `Domain.Status` type is source-breaking.

A `v2.0.0` Git tag/release should only be created after the real consumer validation projects restore and build successfully against the new package line.

## Consumer-driven guidance

当前 Framework 的形态来自真实消费者：普通 Command 使用轻量 Unit of Work 并最终一次 Commit；显式事务只是少数例外。旧版进程内 Event 保留兼容，但跨服务可靠投递使用 Integration Event + Outbox + MQ。
