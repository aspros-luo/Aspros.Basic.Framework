# HTTP Resilience 学习与实战教程

## 1. 先理解问题，而不是背 API

微服务调用失败并不只意味着“代码有 Bug”。常见故障包括网络抖动、DNS/TCP 建连缓慢、下游过载、HTTP 429/408/5xx、连接池耗尽以及请求长时间不返回。

Resilience 的目标不是保证请求永远成功，而是在依赖异常时控制资源和失败传播：

`Concurrency Limit -> Total Timeout -> Retry -> Circuit Breaker -> Attempt Timeout -> Downstream`

Framework 提供统一入口，但业务仍然决定 Retry、Fallback 和幂等策略。

## 2. Framework 当前入口

### Named Client

```csharp
builder.Services.AddFrameworkResilientHttpClient(
    "identity",
    client =>
    {
        client.BaseAddress = new Uri("http://identity-service");

        // Resilience Pipeline 管理超时，避免两套独立 timeout。
        client.Timeout = Timeout.InfiniteTimeSpan;
    });
```

调用：

```csharp
public sealed class IdentityClient(IHttpClientFactory factory)
{
    public async Task<UserIdentityDto?> GetAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var client = factory.CreateClient("identity");

        using var response = await client.GetAsync(
            $"/api/identity/{userId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UserIdentityDto>(
            cancellationToken);
    }
}
```

### Typed Client

更推荐业务服务使用 Typed Client：

```csharp
builder.Services
    .AddHttpClient<IdentityClient>(client =>
    {
        client.BaseAddress = new Uri("http://identity-service");
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .UseFrameworkResilience();
```

这样业务代码不需要自己从 `IHttpClientFactory` 取 Client。

## 3. Timeout

Timeout 回答：

> 一次调用最多允许消耗多少时间？

要区分：

- **Attempt Timeout**：单次尝试最多多久；
- **Total Timeout**：整个 HTTP 操作最多多久，包括 Retry；
- **CancellationToken**：上游主动取消；
- **Business Deadline**：业务自己定义的最终截止时间。

例如：

```text
Total = 10s
Attempt = 2s
Retry = 2
```

意味着不能因为 Retry 而无限等待；所有尝试必须受到总预算约束。

不要同时让 `HttpClient.Timeout` 和 Resilience Pipeline 各自控制超时。推荐把 Client timeout 设为 `Timeout.InfiniteTimeSpan`，由 Pipeline 统一控制。

## 4. Retry

Retry 只适用于“重复执行安全”的操作。

适合：

```text
GET /users/1
GET /products/100
```

谨慎：

```text
PUT /users/1
```

必须根据 API 的实际幂等语义判断。

危险：

```text
POST /orders
POST /payments
POST /inventory/deduct
```

如果第一次请求已经在服务器成功提交，但响应丢失，客户端 Retry 可能制造重复业务。

因此：

> Retry 的前提是幂等，而不是“网络异常”。

对于写操作可以明确禁止 unsafe HTTP methods 的自动 Retry：

```csharp
builder.Services
    .AddHttpClient<OrderClient>(client =>
    {
        client.BaseAddress = new Uri("http://order-service");
    })
    .AddStandardResilienceHandler(options =>
    {
        // POST 等非安全方法默认不自动重试。
        options.Retry.DisableForUnsafeHttpMethods();
    });
```

如果业务真的需要重试写操作，应先设计 Idempotency-Key 或服务端幂等记录。

## 5. Circuit Breaker

Circuit Breaker 解决：

> 下游已经明显不健康时，为什么还要持续打它？

状态：

```text
Closed -> Open -> HalfOpen -> Closed
```

Closed：正常调用并统计失败。

Open：快速失败，暂时不访问下游。

HalfOpen：冷却后允许少量请求探测恢复。

恢复：回到 Closed。

再次失败：回到 Open。

因此：

- Retry = “再试一次”
- Circuit Breaker = “现在先别试”

二者不是同一个功能。

## 6. Concurrency Limiter

并发限制解决：

> 我最多允许多少个调用同时进入下游？

例如 1000 个请求同时需要 Identity：

```text
1000 incoming
      |
      v
concurrency limit
      |
      v
100 downstream calls
```

这既保护下游，也保护自己的连接池、线程池和内存。

## 7. Inbound Rate Limit 与 Outbound Resilience

不要混淆。

入站：

```text
Internet -> RateLimiter -> Controller
```

保护自己的 API。

出站：

```text
User -> HttpClient -> Resilience -> Identity
```

保护自己调用下游的行为。

Framework 两种能力都提供，但策略由业务服务配置。

## 8. Fallback

Fallback 是业务决策，不是“任何错误都返回默认值”。

合理：

```text
Recommendation Service down
        |
        v
cached recommendation
```

危险：

```text
Payment Service down
        |
        v
pretend payment succeeded
```

因此 Framework 不应该规定统一 Fallback 响应。

## 9. Retry Amplification

不要让每一层都 Retry：

```text
Gateway Retry = 3
Framework Retry = 3
SDK Retry = 3
```

理论上一次故障可能产生：

`3 x 3 x 3 = 27`

次下游请求。

生产系统应该明确 Retry ownership。通常应选择一个主要责任层，并让其他层避免重复 Retry。

## 10. 一个真实学习实验

建议使用：

```text
Xr.User -> Xr.Identity
```

建立以下场景：

### Case A：正常

Identity 返回 200。

观察：

- 请求正常；
- 没有 Retry；
- 没有 Circuit Open。

### Case B：Identity 延迟

Identity 故意延迟 5 秒。

观察：

- Attempt Timeout；
- Total Timeout；
- CancellationToken；
- 最终异常类型。

### Case C：Identity 返回 503

连续返回 503。

观察：

- Retry 次数；
- backoff；
- circuit breaker；
- 最终失败。

### Case D：恢复

Identity 从 503 恢复到 200。

观察：

`Open -> HalfOpen -> Closed`

### Case E：POST

模拟：

`POST /orders`

第一次服务端已经写 DB，但客户端没有收到响应。

观察为什么 Retry 可能制造重复订单。

### Case F：Idempotency

增加：

`Idempotency-Key: order-create-xxxx`

服务端保存 command/result。

第二次请求返回第一次结果，而不是再次创建订单。

完成这六个实验后，才算真正理解 HTTP Resilience。

## 11. 学习顺序

1. HttpClientFactory
2. Timeout
3. Retry
4. Idempotency
5. Circuit Breaker
6. Concurrency Limiting
7. Fallback
8. Metrics/Tracing
9. Retry Amplification
10. Hedging

不要从 Polly 内部 API 开始背。

## 12. Framework 的边界

Framework 提供：

```csharp
AddFrameworkResilientHttpClient(...)
UseFrameworkResilience()
```

Framework 不规定：

- 所有服务使用相同 Retry；
- 所有服务使用相同 Timeout；
- 所有 POST 都 Retry；
- 所有失败都 Fallback；
- 业务代码直接依赖 Polly 类型。

目标是让 User、Category、Identity、Trade 等微服务拥有统一基础设施入口，同时保留业务策略的自主权。
