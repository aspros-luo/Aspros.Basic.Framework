# Framework v10 Kernel 设计说明

## 1. 目标

Kernel 是 Framework v10 的领域基础层，提供 DDD 开发所需的轻量通用抽象。

设计原则：

- 不依赖 ORM
- 不依赖消息中间件
- 不依赖 Web 框架
- 保持 Domain 层纯净
- 只提供已经验证有复用价值的基础能力，不为未来业务预先堆叠抽象

## 2. 核心组件

### Entity

实体通过唯一标识区分对象身份。

当前实现采用：

- Id 只读，避免实体身份在生命周期中被意外修改
- 只有相同具体实体类型且 Id 相等时才视为同一实体
- HashCode 与实体具体类型、Id 保持一致

适用于：

- 用户
- 订单
- 商品

### AggregateRoot

聚合根是聚合的一致性边界，并负责保存聚合内部产生的领域事件。

当前实现只维护领域事件集合，不负责事件分发、消息发布或事务提交。事件由 Application / Infrastructure 在 Unit of Work 生命周期中按需处理。

### ValueObject

值对象没有独立身份，通过内部值判断相等性。

当前实现由具体值对象通过 GetEqualityComponents() 提供参与相等性比较的成员。

### Domain Event

领域事件用于描述已经发生的业务事实。

例如：

- UserCreated
- OrderPaid
- PaymentCompleted

当前 Kernel 只定义事件本身及发生时间，不负责事件分发。

后续如果真实业务出现跨聚合、跨进程消息一致性需求，再由 Application / Infrastructure 层接入对应机制。

### Result

Result / Result<T> 用于表达可以预期的业务结果，避免使用异常控制正常业务分支。

它不负责 HTTP 状态码、API 响应格式等传输层语义。


## 3.1 Legacy AggregateRoot compatibility

Framework v10 intentionally supports two aggregate-root generations during migration.

The legacy contract is the empty marker:

`Aspros.Base.Framework.Domain.IAggregateRoot`

The new Kernel contract is:

`Aspros.Base.Framework.Domain.Kernel.IAggregateRoot`

and the new Kernel contract inherits the legacy marker.

Therefore:

```text
Kernel.IAggregateRoot
        ↓
Legacy IAggregateRoot
```

This is a one-way compatibility bridge.

- Existing consumers that implement the legacy marker continue to satisfy the existing `IRepository<T>` constraint.
- New Kernel aggregate roots are also valid legacy aggregate roots.
- Legacy aggregate roots do not gain Domain Event state implicitly.
- Only aggregates based on the new Kernel contract participate in the new Domain Event collection and dispatch lifecycle.
- Framework Infrastructure must not infer Domain Events from the old empty marker.

This allows Xr.User / Xr.Category-style legacy aggregates to migrate incrementally without forcing Framework to manufacture fake Domain Event behavior for existing entities.

The intended migration path is:

```text
Legacy AggregateRoot
        ↓
Consumer incrementally adopts Kernel.AggregateRoot<TId>
        ↓
Domain Events become available
        ↓
New Application / Infrastructure event lifecycle
```

The compatibility bridge is therefore a migration mechanism, not a second DDD model.

## 2.1 Legacy 持久化实体

Framework 目前还保留两套历史持久化基类：

- `BaseEntity`：旧版审计字段命名为 `Creator/CreateTime/Updater/UpdateTime/Deleted`；
- `BasicEntity`：新版审计字段命名为 `Creator/GmtCreated/Modifier/GmtModified/IsDeleted`，并提供 Framework 生命周期 `EntityStatus`。

两者都通过 `IAuditableEntity` 暴露相同的审计语义，Infrastructure 的 Unit of Work 只依赖这套统一契约。

这些类型属于兼容性的持久化实体模型，不是 Kernel `Entity<TId>` 的替代品。

当前真实消费者已经分别使用两种形式，因此 v10 暂时让它们并存：

```text
Legacy Consumer Aggregate
        ↓
BaseEntity / BasicEntity
        ↓
IAuditableEntity
        ↓
Infrastructure 持久化

New DDD Aggregate
        ↓
Kernel.Entity<TId> / AggregateRoot<TId>
```

不要仅仅为了减少基类数量而强行合并它们。未来是否统一，应由真实迁移收益决定。

## 3. 当前边界

Kernel 当前只保留：

- Entity
- AggregateRoot
- ValueObject
- Domain Event
- Error
- Result

暂不加入：

- Specification
- Repository
- Unit of Work
- Domain Service
- Event Bus
- Message Bus
- RPC
- 复杂缓存抽象

这些能力只有在实际业务形成稳定重复需求后，才进入 Framework 的公共层。

## 4. 演进原则

Kernel 的演进遵循：

> 先解决真实重复问题，再抽象为 Framework 能力。

因此，新增抽象需要同时满足：

1. 已经出现真实业务场景；
2. 多个业务模块存在重复实现；
3. 抽象后能够降低长期维护成本；
4. 不破坏 Domain 对具体基础设施的独立性。
