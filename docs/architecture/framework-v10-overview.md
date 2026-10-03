# Aspros Basic Framework v10 Overview

## Goal

Framework v10 is an incremental evolution of Aspros Basic Framework toward a modular application foundation.

## Design Goals

- Clear domain boundaries
- DDD-friendly primitives
- Lightweight application abstractions
- Extensible infrastructure integrations
- Keep migration cost controlled
- Prefer real business needs over speculative framework features

## Architecture Direction

The current architecture contains:

- Domain Kernel
- Application Layer
- Infrastructure Abstractions
- Persistence
- Messaging
- Optional cross-cutting Pipeline capabilities

The framework intentionally does not create a heavyweight generic Application Runtime.

## Domain Event and Integration Event

Domain Events are raised by Aggregate Roots and remain independent from infrastructure.

```text
AggregateRoot
     |
     v
Domain Event
     |
     v
Application Domain Event Handler
     |
     +--> local/domain-side effects
     |
     +--> IIntegrationEventPublisher
                    |
                    v
               CAP Outbox
                    |
                    v
               Message Broker
```

Domain Event and Integration Event are deliberately different concepts:

- **Domain Event**: in-process domain semantics. It is not a reliable cross-service delivery mechanism.
- **Integration Event**: cross-process or cross-service communication. When delivery must survive process failure, use the CAP / Outbox / MQ path.
- A Domain Event Handler may publish an Integration Event when the business boundary requires it.
- CommitAsync only performs the direct EF Core persistence commit and does not automatically dispatch Domain Events.
- `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)` 是多步骤业务确实需要全部成功或全部失败时的显式选择，并不是每个 Command 的默认步骤。
- 当持久化变更、Domain Event 处理以及可靠 Integration Event 发布必须处于同一事务边界时，才使用 `ITransactionalUnitOfWork`。

When `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)` is used, Domain Event handling occurs inside the local Unit of Work transaction. With CAP transaction integration enabled, the business data and Outbox record are committed together. CAP documents EF Core transaction integration through `ICapPublisher`.

## RPC Boundary

Framework v10 不定义通用 RPC Client 抽象，因为本轮审查的真实消费者目前没有稳定的通用 RPC 需求。

出现真实同步跨服务调用时，应在 Application 定义业务专属 Port，在 Infrastructure 使用实际协议实现。新的内部同步服务调用，在双方可以共享 protobuf 契约时，优先考虑 gRPC。

核心 Framework 不预先引入通用 RPC 注册中心、服务发现、负载均衡、重试或序列化抽象；只有多个消费者形成稳定重复需求后才上提。

## Persistence Strategy

Framework v10 does not force a single data-access technology.

### EF Core

EF Core remains the default persistence approach for normal transactional business data.

Typical use cases:

- Aggregate persistence
- Change tracking
- Standard CRUD
- Transactional application workflows
- MySQL relational persistence already used by the framework

### Dapper

Dapper is an optional direct dependency of a consuming service when EF Core is not the right tool.

Typical use cases:

- Complex SQL
- Reporting-style queries
- Performance-sensitive read paths
- SQL that would become unnecessarily complicated through LINQ
- Existing hand-written SQL that should remain explicit

Dapper does not replace EF Core and is not exposed from the Domain layer.

### ClickHouse

The official ClickHouse .NET driver is an optional direct dependency for analytical workloads.

Typical use cases:

- User behavior/event analysis
- Large-volume analytical queries
- User profiling
- Recommendation and “千人千面” scenarios
- Aggregation and segmentation workloads that should not burden the transactional database

ClickHouse is treated as an analytical data store rather than another transactional ORM.

```text
                    Application
                        |
          +-------------+-------------+
          |                           |
   Transactional Data          Analytical Data
          |                           |
       EF Core                    ClickHouse
          |                           |
        MySQL                Behavior / Profile
                                    |
                             Recommendation
```

Dapper can be used alongside EF Core when a specific SQL path requires it:

```text
Application
    |
    +--> normal persistence --> EF Core --> MySQL
    |
    +--> specialized SQL ----> Dapper --> relational DB
    |
    +--> analytics/profile --> ClickHouse Driver --> ClickHouse
```

The framework intentionally does not introduce a generic “data access” abstraction that hides these technologies. A consuming service declares Dapper or ClickHouse directly when it needs them.

## Transaction Choice

Unit of Work 不意味着每个业务操作都必须启动显式数据库事务。简单的单表或单次保存业务直接使用 `IUnitOfWork` 并最终调用一次 `CommitAsync()`；只有要求多步骤全部成功或全部失败的业务才显式依赖 `ITransactionalUnitOfWork`。

The business layer chooses the path. Domain Events are also optional and should only be introduced when a meaningful domain fact requires additional reactions.

显式的 `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)` 事务范围负责当前 `DbContext` 的事务。 Nested calls are intentionally rejected rather than pretending to provide nested database transactions; inner application operations should participate in the outer transaction.



轻量 Unit of Work 是应用层持久化边界。

普通 Command 使用 `IUnitOfWork`，通过 `Register*` 登记变更，最后调用一次 `CommitAsync()`。只有确实需要本地多步原子性的流程才依赖 `ITransactionalUnitOfWork`。

对于显式事务 Command：

```text
Command
  ↓
ExecuteInTransactionAsync
  ↓
Domain / Persistence Operations
  ↓
SaveChanges
  ↓
Domain Event Dispatch
  ↓
Handler / Integration Event
  ↓
SaveChanges
  ↓
Commit
```

EF Core supports multiple SaveChanges calls inside an explicit transaction, which allows domain event handlers to participate in the same transaction boundary.

## Migration Principle

Existing capabilities are preserved first. Refactoring happens through incremental migration rather than a destructive rewrite.


## Consumer-driven conclusions

The real repositories changed the Framework priorities:

1. EF Core persistence and repository queries are common enough to remain first-class.
2. Explicit local transactions exist, but only for a small subset of workflows.
3. In-process events are a compatibility requirement; reliable cross-service events belong to CAP/Outbox/MQ.
4. WorkContext is a request-context abstraction, not a JWT parser or Redis cache.
5. Permission checking is a business-side policy plugged into generic Framework middleware.
6. The Framework package should not provide unrelated transitive dependencies merely because one service happens to use them.

## 真实消费者基线

当前 API 形态来自 `Xr.User` 与 `Xr.Category` 的真实使用：Repository 查询对象、`RegisterNew/Dirty/Delete`、最终一次 `CommitAsync()`，以及只有业务本身需要时才使用的本地显式事务。`Xr.Identity` 当前没有消费 Framework，因此保持独立。

## gRPC 决策

只有真实的同步跨服务业务契约出现时才使用 gRPC。当前审查的 Xr.User / Xr.Category / Xr.Identity 没有符合条件的业务调用，因此本轮没有添加演示性 gRPC Client。
