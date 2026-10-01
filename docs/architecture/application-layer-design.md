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


## 8. RPC 调用边界

Framework v10 保留 RPC 能力，但只在 Application 层定义最小调用契约：

`IRpcClient`

Application 通过该契约表达对远程服务的调用需求，不直接依赖 gRPC、Dubbo 或其他具体 RPC 框架。

具体实现放在 Infrastructure 层：

```
Application
    │
    │ IRpcClient
    ▼
Infrastructure
    │
    ├── gRPC
    ├── Dubbo
    └── 其他 RPC 实现
```

当前不额外定义：

- RPC 注册中心抽象
- RPC 服务发现抽象
- RPC 负载均衡抽象
- RPC 重试/熔断框架
- RPC 序列化协议抽象

这些能力优先复用实际采用的 RPC 框架。只有多个业务项目形成稳定重复需求时，再向 Framework 上提公共抽象。

RPC 不改变 Application 的核心职责：Handler 负责业务用例编排，RPC 只是其中一种基础设施依赖。
