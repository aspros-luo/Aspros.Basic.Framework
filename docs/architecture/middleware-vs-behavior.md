# Middleware 与 Application Behavior 边界

## 1. 为什么两者都存在

Middleware 与 Application Behavior 都可以承载横切逻辑，但它们解决的是不同层次的问题。

```text
HTTP / Transport
      ↓
ASP.NET Core Middleware
      ↓
Command / Query
      ↓
Application Pipeline
      ↓
Handler
      ↓
Domain
```

### Middleware

Middleware 关注的是 HTTP 请求生命周期，例如：

- 身份认证
- 异常处理
- HTTP 日志
- Trace / OpenTelemetry
- Header
- CORS
- 限流

这些能力通常不应该进入 Application 层。

### Application Behavior

Behavior 关注的是一次 Command / Query 用例的执行生命周期。

同一个 Application Handler 未来可能来自 HTTP API、MQ Consumer、RPC、Background Job 或定时任务，因此真正属于用例执行的横切能力，不应该绑定 HTTP。

## 2. Framework v10 的取舍

Framework 不默认把所有横切能力都塞进 Behavior。

当前只保留 Pipeline 机制，不强制提供大量内置 Behavior。

### 高价值、适合 Framework 提供抽象

- Transaction
- Idempotency
- Outbox / Domain Event integration

这些能力通常与业务执行的一致性有关，而且基础设施实现可以因项目不同而变化，因此 Framework 更适合提供稳定的抽象边界。

### 保持为扩展点

- Validation
- Authorization
- Logging
- Metrics
- Tracing

这些能力已经有成熟的 ASP.NET Core、DataAnnotations、FluentValidation、ILogger、OpenTelemetry 等方案，不应该为了“框架完整”重复造轮子。

## 3. 当前不做的事情

Framework v10 当前不增加：

- 自研完整 Validation Framework
- 自研 Logging Framework
- 自研 Metrics Framework
- 自研 Tracing Framework
- 强制所有请求经过一组固定 Behavior
- 为未来可能使用的 RPC 提前加入 RPC 抽象

原则是：**先提供稳定的最小基础设施，等真实业务出现重复问题后再抽象。**

## 4. 一个重要判断标准

新增 Framework 能力前先问三个问题：

1. 现有 ASP.NET Core / .NET 能力是否已经解决？
2. 是否已经在多个业务中重复出现？
3. 如果现在不抽象，后续迁移成本是否明显？

只有确实值得统一时才进入 Framework。
