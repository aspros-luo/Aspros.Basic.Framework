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
        +--> Outbox / Domain Event（按需）
        |
        v
Handler
        |
        v
Domain
```

## Transaction

Transaction 是 Framework 值得优先抽象的能力，因为它描述的是一次 Application Command 的一致性边界。

当前通过 IUnitOfWork.ExecuteInTransactionAsync(...) 提供最小事务边界，而不是额外定义 ITransaction 或 ITransactionManager。

事务操作成功后，Infrastructure 会自动执行一次 SaveChanges 并提交事务；异常则回滚。因此调用方不需要为了事务包装再次手动 Commit。

```text
Command
  ↓
ExecuteInTransactionAsync
  ↓
Handler / Persistence Operations
  ↓
SaveChanges
  ↓
Commit Transaction
```

## Outbox / Domain Event

Domain 层只负责产生领域事件。Application 层负责决定何时处理这些事件，以及如何与事务边界协作。Infrastructure 再负责具体持久化和消息发布实现。

Framework 已经依赖 DotNetCore.CAP。CAP 提供 Outbox / 本地消息表能力，并支持把 EF Core 数据库事务与消息发布绑定。

因此 Infrastructure 的 EfUnitOfWork 在检测到 ICapPublisher 时，会使用 CAP 的 EF Core transaction integration；在同一个事务中发布的集成事件与业务数据一起提交或回滚。CAP 的官方示例也采用 EF Core Database.BeginTransaction 与 ICapPublisher 绑定的方式。

Application 只看到 IIntegrationEventPublisher，不直接依赖 CAP。

## Idempotency

对于消息消费、支付回调、重试请求等场景，同一个 Command 可能被重复执行。Idempotency 属于用例执行层的能力，可以通过 Behavior 统一接入，但存储实现不应该进入 Application 层。

## Unit of Work

Framework v10 保留轻量 Unit of Work 能力，用于表达一次应用用例的持久化提交边界。

当前定义：
- CommitAsync()：提交当前工作单元中的持久化变更；
- ExecuteInTransactionAsync(...)：在一个数据库事务中执行多个持久化操作，并在成功结束时自动 SaveChanges + Commit；
- 泛型事务版本：允许事务操作返回结果。

Unit of Work 不负责：
- 定义 Repository；
- 暴露 DbContext；
- 管理数据库连接；
- 把 EF Core 类型泄漏到 Application；
- 在 Application 层定义具体数据库事务类型。

```text
Command Handler
      ↓
IUnitOfWork.ExecuteInTransactionAsync(...)
      ↓
Domain / Persistence Operations
      ↓
IIntegrationEventPublisher（可选）
      ↓
EF Core SaveChanges + CAP Outbox
      ↓
Commit
```

当前仍然不增加：
- ITransaction
- ITransactionManager
- TransactionScope 抽象
- 自定义数据库事务接口

这样既提供了真正可用的事务与 Outbox 组合，又避免把 EF Core 和 CAP 再包装成一套重复的数据库事务模型。