# Application Layer 设计说明

## 目标

Application Layer 负责业务流程编排，不包含核心业务规则。

## 分层关系

```text
API / MQ / Job
     |
     v
Command / Query
     |
     v
Application Handler
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

负责协调领域对象、仓储抽象以及外部服务抽象。

## 当前实现策略

Framework v10 当前只建立最小 Application 基础：

- Command / Query 抽象
- Handler 抽象
- 可选 Pipeline 机制

Validation、Transaction、Idempotency、Outbox 等能力只有在实际业务出现稳定需求后再逐步接入。

不会为了架构完整而一次性引入完整 Runtime、RPC、验证框架或消息抽象。
