# Application Pipeline 设计说明

## 目标

Application Pipeline 用于承载 **Application 用例执行层** 的横切能力，避免业务 Handler 直接依赖基础设施实现。

Pipeline 与 ASP.NET Core Middleware 不是同一层：Middleware 负责 HTTP / Transport 生命周期，Pipeline 负责 Command / Query 用例生命周期。

## 执行流程

```text
HTTP / MQ / Job / RPC
        |
        v
Command / Query
        |
        v
Application Pipeline
        |
        +--> Transaction（按需）
        |
        +--> Idempotency（按需）
        |
        +--> Domain Event（事务内按需）
        |
        +--> Integration Event / Outbox（由 Domain Event Handler 按需触发）
        |
        v
Handler
        |
        v
Domain
```

## Transaction

Transaction 是 Framework 值得优先抽象的能力，因为它描述的是一次 Application Command 的一致性边界。

当前通过 `IUnitOfWork.ExecuteInTransactionAsync(...)` 提供最小事务边界，而不是额外定义 `ITransaction` 或 `ITransactionManager`。

事务操作成功后，Infrastructure 会自动执行持久化、领域事件分发以及最终 Commit；异常则回滚。

## Domain Event

Domain Event 由 AggregateRoot 在领域状态发生重要变化时产生。

Domain 层只负责：

- 定义 `IDomainEvent`；
- 在 AggregateRoot 内产生事件；
- 保存尚未处理的事件。

Domain 层不依赖：

- MediatR；
- CAP；
- RabbitMQ；
- Kafka；
- HTTP；
- 其他基础设施。

Application 层提供：

- `IDomainEventHandler<TDomainEvent>`
- `IDomainEventDispatcher`

Infrastructure 提供默认 DI 分发实现。

### 事务内处理顺序

```text
Command Handler
      ↓
IUnitOfWork.ExecuteInTransactionAsync(...)
      ↓
Domain / Persistence Operations
      ↓
EF Core SaveChanges
      ↓
Dispatch Domain Events
      ↓
Domain Event Handler
      ↓
IIntegrationEventPublisher（可选）
      ↓
再次 SaveChanges
      ↓
Commit
```

领域事件在显式 Unit of Work 事务中处理，因此领域事件 Handler 对当前 DbContext 的修改仍属于同一个数据库事务。

如果 Handler 发布 Integration Event，并且当前 Unit of Work 使用 CAP transaction integration，则业务数据与 CAP Outbox 记录一起提交或回滚。CAP 官方文档支持将 EF Core transaction 与 `ICapPublisher` 绑定。

Framework 不把 Domain Event 自动等同于 Integration Event：

- **Domain Event**：进程内、领域语义、同步处理；
- **Integration Event**：跨进程/跨服务通信，使用 `IIntegrationEventPublisher`；
- Domain Event Handler 可以根据业务需要发布 Integration Event，但不是每个 Domain Event 都必须进入消息队列。

### 级联领域事件

Domain Event Handler 可以继续产生新的 Domain Event。

Infrastructure 会持续处理当前 AggregateRoot 上尚未分发的事件，直到事件队列为空，然后再次执行 `SaveChanges`。

因此可以支持：

```text
Event A
  ↓
Handler A
  ↓
Event B
  ↓
Handler B
```

所有这些操作仍位于当前 Unit of Work 事务中。

## Outbox

Framework 已经依赖 DotNetCore.CAP。CAP 提供本地消息表 / Outbox 能力，并支持把 EF Core 数据库事务与消息发布绑定。

Application 只看到 `IIntegrationEventPublisher`，不直接依赖 CAP。

因此推荐的完整路径是：

```text
Aggregate
   ↓
Domain Event
   ↓
Domain Event Handler
   ↓
IIntegrationEventPublisher
   ↓
CAP
   ↓
Outbox
   ↓
Message Broker
```

## Idempotency

对于消息消费、支付回调、重试请求等场景，同一个 Command 可能被重复执行。Idempotency 属于用例执行层的能力，可以通过 Behavior 统一接入，但存储实现不应该进入 Application 层。

## Unit of Work

Framework v10 保留轻量 Unit of Work 能力，用于表达一次应用用例的持久化提交边界。

当前定义：

- `CommitAsync()`：提交当前工作单元中的持久化变更；
- `ExecuteInTransactionAsync(...)`：在一个数据库事务中执行多个持久化操作，并在成功结束时自动处理 Domain Event、SaveChanges + Commit；
- 泛型事务版本：允许事务操作返回结果。

涉及 Domain Event / Outbox 的 Command 应优先使用 `ExecuteInTransactionAsync(...)`，以保持领域状态、Handler 引起的数据库变更以及 CAP Outbox 处于同一事务边界。

Unit of Work 不负责：

- 定义 Repository；
- 暴露 DbContext；
- 管理数据库连接；
- 把 EF Core 类型泄漏到 Application；
- 在 Application 层定义具体数据库事务类型。

当前仍然不增加：

- `ITransaction`
- `ITransactionManager`
- `TransactionScope` 抽象
- 自定义数据库事务接口

这样既提供了真正可用的 Domain Event → Integration Event → Outbox 闭环，又避免把 EF Core 和 CAP 再包装成一套重复的基础设施模型。


## Runtime Implementation

Framework does not maintain a second request dispatcher.

The existing `ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`, and `IPipelineBehavior` contracts are now directly wired into MediatR:

```text
HTTP / MQ / Job / RPC
        |
        v
Command / Query
        |
        v
MediatR
        |
        +--> IPipelineBehavior
        |
        v
Handler
        |
        v
Domain
```

Business handlers continue to expose `HandleAsync(...)`. Default interface implementations adapt them to MediatR's `Handle(...)`, so business code does not need a second handler method.

MediatR 12.1+ no longer automatically scans Pipeline Behaviors, so Framework `AutoInject()` explicitly discovers open generic Behaviors and registers them in deterministic type-name order. This keeps the existing auto-injection model while making the Pipeline actually executable.

### Handler Contracts

- `ICommand`: state-changing request;
- `ICommand<TResult>`: state-changing request with a response;
- `IQuery<TResult>`: read-only request;
- `ICommandHandler<TCommand>` / `ICommandHandler<TCommand, TResult>`: Command handlers;
- `IQueryHandler<TQuery, TResult>`: Query handlers.

Commands and Queries are MediatR Requests and can therefore enter the Pipeline through `IMediator` / `ISender`.

### Pipeline Behavior

Framework `IPipelineBehavior<TRequest, TResponse>` directly extends MediatR's Pipeline Behavior. The framework does not introduce another execution engine.

Only concrete Behaviors required by real application scenarios should be added. Logging, validation, caching, idempotency, and transaction behaviors are not enabled speculatively.

MediatR requires explicit Behavior registration from 12.1 onward; Framework handles that registration centrally.
