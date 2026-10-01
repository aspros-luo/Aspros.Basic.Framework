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

## 为什么不把 Validation 作为核心 Behavior

字段校验属于输入边界问题，通常可以交给 ASP.NET Core Model Validation、DataAnnotations 或 FluentValidation 等组件。

Application Behavior 可以支持验证扩展，但 Framework 不强制要求所有项目使用自带 Validator，也不提供一套完整的验证规则体系。

## Transaction

Transaction 是 Framework 值得优先抽象的能力，因为它描述的是一次 Application Command 的一致性边界。

典型流程：

```text
Command
  ↓
Begin Transaction
  ↓
Handler
  ↓
Domain State Change
  ↓
Outbox / Domain Event
  ↓
Commit
```

具体数据库事务由 Infrastructure 实现，Application 层只依赖抽象。

## Idempotency

对于消息消费、支付回调、重试请求等场景，同一个 Command 可能被重复执行。

Idempotency 属于用例执行层的能力，可以通过 Behavior 统一接入，但存储实现不应该进入 Application 层。

## Outbox / Domain Event

Domain 层只负责产生领域事件。

Application 层负责决定何时处理这些事件，以及如何与事务边界协作。

Infrastructure 再负责具体持久化和消息发布实现。

## 设计原则

1. Application 层负责业务流程编排。
2. Domain 层负责业务规则。
3. Middleware 负责 Transport 生命周期。
4. Pipeline 负责 Application 用例生命周期。
5. Framework 提供抽象，不重复实现已有成熟组件。
6. 没有真实业务需求时，不提前增加 RPC、复杂缓存、复杂验证等能力。
7. 不把 Behavior 设计成“所有横切逻辑的垃圾桶”。


## 8. Unit of Work

Framework v10 保留轻量 Unit of Work 能力，用于表达一次应用用例的持久化提交边界。

当前只定义：

`IUnitOfWork.CommitAsync()`

其职责是：

- 提交当前用例产生的持久化变更；
- 返回实际提交的变更数量；
- 接受 CancellationToken。

Unit of Work 不负责：

- 定义 Repository；
- 暴露 DbContext；
- 管理数据库连接；
- 把 EF Core 类型泄漏到 Application；
- 自己实现事务机制。

事务的一致性由 Infrastructure 的具体持久化实现负责。

典型调用关系：

```
Command Handler
      ↓
 Domain / Repository
      ↓
 IUnitOfWork.CommitAsync()
      ↓
Infrastructure
      ↓
EF Core / Database Transaction
```

如果未来真实业务出现“一个用例需要多个持久化操作必须原子提交”的场景，再由 Infrastructure 为 Unit of Work 提供事务实现。

因此当前不额外增加：

- ITransaction
- ITransactionManager
- TransactionScope 抽象
- 自定义数据库事务接口

避免在没有实际业务需求时重复抽象 EF Core 已经提供的事务能力。
