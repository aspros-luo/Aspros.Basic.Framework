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

## Pipeline 的职责边界

Pipeline 本身只提供组合机制，不默认实现大量横切功能。

适合由 Framework 提供抽象的能力：
- Transaction
- Idempotency
- Outbox / Domain Event integration

适合作为扩展点的能力：
- Validation
- Authorization
- Logging
- Metrics
- Tracing

原因是这些能力已经存在成熟的 .NET / ASP.NET Core 组件。Framework 不应该为了架构完整而重复实现。

## Transaction

Transaction 是 Framework 值得优先抽象的能力，因为它描述的是一次 Application Command 的一致性边界。

当前通过 IUnitOfWork.ExecuteInTransactionAsync(...) 提供最小事务边界，而不是额外定义 ITransaction 或 ITransactionManager。

典型流程：

```text
Command
  ↓
ExecuteInTransactionAsync
  ↓
Handler / Persistence Operations
  ↓
IUnitOfWork.CommitAsync()
  ↓
Commit Transaction
```

具体数据库事务由 Infrastructure 实现，Application 层只依赖 IUnitOfWork。

对于单次 CommitAsync()，EF Core 自身负责单次 SaveChanges 的原子性；显式事务主要用于一个用例需要多个持久化操作必须作为一个整体提交的场景。

## Idempotency

对于消息消费、支付回调、重试请求等场景，同一个 Command 可能被重复执行。

Idempotency 属于用例执行层的能力，可以通过 Behavior 统一接入，但存储实现不应该进入 Application 层。

## Outbox / Domain Event

Domain 层只负责产生领域事件。

Application 层负责决定何时处理这些事件，以及如何与事务边界协作。

Infrastructure 再负责具体持久化和消息发布实现。

当前仓库已经依赖 DotNetCore.CAP。CAP 本身提供 Outbox / 本地消息表能力，并支持将 EF Core 数据库事务与消息发布绑定；因此后续真正接入 Outbox 时优先复用现有 CAP，而不是重新实现一套 Outbox 表和消息投递机制。

## 设计原则

1. Application 层负责业务流程编排。
2. Domain 层负责业务规则。
3. Middleware 负责 Transport 生命周期。
4. Pipeline 负责 Application 用例生命周期。
5. Framework 提供抽象，不重复实现已有成熟组件。
6. 没有真实业务需求时，不提前增加复杂缓存、复杂验证等能力。
7. 不把 Behavior 设计成“所有横切逻辑的垃圾桶”。

## 8. Unit of Work

Framework v10 保留轻量 Unit of Work 能力，用于表达一次应用用例的持久化提交边界。

当前定义：
- CommitAsync()：提交当前工作单元中的持久化变更；
- ExecuteInTransactionAsync(...)：在一个数据库事务中执行多个持久化操作；
- 泛型事务版本：允许事务操作返回结果。

Unit of Work 不负责：
- 定义 Repository；
- 暴露 DbContext；
- 管理数据库连接；
- 把 EF Core 类型泄漏到 Application；
- 在 Application 层定义具体数据库事务类型。

典型调用关系：

```text
Command Handler
      ↓
IUnitOfWork.ExecuteInTransactionAsync(...)
      ↓
Domain / Persistence Operations
      ↓
IUnitOfWork.CommitAsync()
      ↓
Infrastructure
      ↓
EF Core / Database Transaction
```

当前 Infrastructure 使用 EF Core Database.BeginTransactionAsync() 实现显式事务；事务提交或异常回滚完全由 Infrastructure 管理。

因此当前仍然不增加：
- ITransaction
- ITransactionManager
- TransactionScope 抽象
- 自定义数据库事务接口

这样既提供了真正可用的事务边界，又避免把 EF Core 已经提供的事务模型再次包装成一套框架类型。