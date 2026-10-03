# Application Layer 设计说明

## 目标

Application Layer 负责业务流程编排，不包含核心业务规则。

## 分层关系

```text
API / MQ / Job / RPC
     |
     v
Command / Query
     |
     v
Application Handler
     |
     +--> Domain Event Handler（按需）
     |
     +--> Integration Event Publisher（按需）
     |
     v
Domain
     |
     v
Infrastructure 实现由 Application 抽象间接使用
```

实际依赖方向保持为：

```text
Infrastructure → Application → Domain
```

Application 不应该反向依赖具体数据库、消息队列、HTTP 客户端或其他基础设施实现。

## Command

用于修改系统状态的业务操作。

## Query

用于查询数据，不改变领域状态。

## Handler

负责协调领域对象、持久化抽象以及外部服务抽象。

## Persistence and Transaction Choice

Not every Command needs an explicit database transaction or a Domain Event.

For simple business:

```text
Controller
   ↓
CommandHandler / Application Service
   ↓
EF Core
   ↓
IUnitOfWork.CommitAsync()
   ↓
Database
```

For multi-step business that requires all-or-nothing behavior, the handler explicitly uses `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)`.

An explicit transaction scope must be the outermost scope for the current `DbContext`. The Framework intentionally does not emulate nested database transactions: calling `ExecuteInTransactionAsync(...)` while the same `DbContext` already has an active transaction fails immediately with a clear configuration error. Compose the inner work inside the existing transaction instead.

## Domain Event

Application 提供最小领域事件处理边界：

- `IDomainEventHandler<TDomainEvent>`
- `IDomainEventDispatcher`

Domain Event Handler 用于处理聚合已经产生的领域事件。

它可以：

- 更新其他需要同步维护的领域状态；
- 调用 Application 层抽象；
- 发布 Integration Event；
- 协调其他用例级能力。

它不应该把 CAP、RabbitMQ 等具体实现直接带入 Domain。

## Integration Event

Application 使用 `IIntegrationEventPublisher` 表达跨服务消息发布需求。

具体实现位于 Infrastructure，例如当前的 CAP 实现。

因此：

```text
Domain Event
      ↓
Application Handler
      ↓
IIntegrationEventPublisher
      ↓
Infrastructure / CAP
```

Domain Event 与 Integration Event 保持语义分离，而不是把所有领域事件直接发送到消息队列。

## ## Unit of Work

Application 层的 `IUnitOfWork` 是轻量持久化会话，提供 `RegisterNew`、`RegisterRangeNew`、`RegisterDirty`、`RegisterRangeDirty`、`RegisterDeleted`、`RegisterRangeDeleted` 与 `CommitAsync`。`Register*` 只登记 EF Core 变更，不执行数据库提交；正常用例最后调用一次 `CommitAsync()`。

只有确实要求本地多步操作全部成功或全部失败的少数用例，才依赖 `ITransactionalUnitOfWork` 并调用 `ExecuteInTransactionAsync(...)`。

当前实现策略

Framework v10 当前只建立最小 Application 基础：

- Command / Query 抽象
- Handler 抽象
- 可选 Pipeline 机制
- Domain Event Handler / Dispatcher
- Integration Event Publisher

Validation、Idempotency、更多 Pipeline Behavior 等能力只有在实际业务出现稳定需求后再逐步接入。

不会为了架构完整而一次性引入完整 Runtime、验证框架或消息抽象。

## RPC 调用边界

Framework v10 保留 RPC 能力，但只在 Application 层定义最小调用契约：

`IRpcClient`

Application 通过该契约表达对远程服务的调用需求，不直接依赖 gRPC、Dubbo 或其他具体 RPC 框架。

具体实现放在 Infrastructure 层：

```text
Application
    |
    | IRpcClient
    v
Infrastructure
    |
    +-- gRPC
    +-- Dubbo
    +-- 其他 RPC 实现
```

当前不额外定义：

- RPC 注册中心抽象
- RPC 服务发现抽象
- RPC 负载均衡抽象
- RPC 重试/熔断框架
- RPC 序列化协议抽象

这些能力优先复用实际采用的 RPC 框架。只有多个业务项目形成稳定重复需求时，再向 Framework 上提公共抽象。

RPC 不改变 Application 的核心职责：Handler 负责业务用例编排，RPC 只是其中一种基础设施依赖。


## Pipeline Runtime

The Application Pipeline is backed by the existing MediatR runtime rather than a second Framework dispatcher.

`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`, and `IPipelineBehavior` are directly compatible with MediatR. Business handlers may keep the Framework-level `HandleAsync(...)` method; default interface implementations adapt it to MediatR.

Framework `AutoInject()` explicitly registers discovered open generic Pipeline Behaviors because MediatR 12.1+ no longer scans Behaviors automatically.

## gRPC 决策

只有真实的同步跨服务业务契约出现时才使用 gRPC。当前审查的 Xr.User / Xr.Category / Xr.Identity 没有符合条件的业务调用，因此本轮没有添加演示性 gRPC Client。
