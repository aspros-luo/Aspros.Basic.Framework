# HTTP Resilience Tutorial

This tutorial focuses on failure semantics rather than memorizing Polly APIs.

## 1. The problem

Microservice calls can fail because of transient network errors, slow DNS/TCP/TLS establishment, downstream overload, HTTP 429/408/5xx, exhausted connection pools, or hung requests.

Resilience does not guarantee success. It controls resource usage and failure propagation:

`Concurrency Limit -> Total Timeout -> Retry -> Circuit Breaker -> Attempt Timeout -> Downstream`

The Framework supplies the infrastructure entry point; each service owns retry, fallback and idempotency decisions.

## 2. Framework usage

### Named client

```csharp
builder.Services.AddFrameworkResilientHttpClient(
    "identity",
    client =>
    {
        client.BaseAddress = new Uri("http://identity-service");

        // Let the resilience pipeline own timeout policy.
        client.Timeout = Timeout.InfiniteTimeSpan;
    });
```

### Typed client

Typed clients are usually preferable for business integrations:

```csharp
builder.Services
    .AddHttpClient<IdentityClient>(client =>
    {
        client.BaseAddress = new Uri("http://identity-service");
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .UseFrameworkResilience();
```

## 3. Timeout

Distinguish:

- **Attempt Timeout**: maximum duration of one attempt.
- **Total Timeout**: maximum duration of the complete HTTP operation, including retries.
- **CancellationToken**: caller cancellation.
- **Business Deadline**: an application-level deadline.

For example:

```text
Total = 10s
Attempt = 2s
Retry = 2
```

All attempts must fit inside the total budget.

Avoid having both `HttpClient.Timeout` and the resilience pipeline independently enforce timeouts. Let the pipeline own the policy and use `Timeout.InfiniteTimeSpan` on the client.

## 4. Retry

Retry only when repeating the operation is safe.

Typical candidates:

```text
GET /users/1
GET /products/100
```

PUT can be retryable only when its API semantics are actually idempotent.

Be careful with:

```text
POST /orders
POST /payments
POST /inventory/deduct
```

If the server committed the operation but the response was lost, a retry can create a duplicate business operation.

The key rule is:

> Retry requires safe repetition or a reliable idempotency mechanism.

For write-heavy clients, disable automatic retries for unsafe methods:

```csharp
builder.Services
    .AddHttpClient<OrderClient>(client =>
    {
        client.BaseAddress = new Uri("http://order-service");
    })
    .AddStandardResilienceHandler(options =>
    {
        // Do not automatically retry POST and other unsafe methods.
        options.Retry.DisableForUnsafeHttpMethods();
    });
```

If a write really needs retry, introduce an Idempotency-Key or server-side command/result record first.

## 5. Circuit Breaker

Circuit Breaker asks:

> If the dependency is clearly unhealthy, why keep calling it?

State machine:

```text
Closed -> Open -> HalfOpen -> Closed
```

Closed: calls flow normally and failures are measured.

Open: calls fail fast for a cooling period.

HalfOpen: a limited probe checks whether the dependency recovered.

Success returns to Closed. Failure returns to Open.

Remember:

- Retry = “try again.”
- Circuit Breaker = “stop trying for a while.”

## 6. Concurrency Limiting

Concurrency limiting asks:

> How many downstream calls can be in flight at once?

For example:

```text
1000 incoming requests
        |
        v
concurrency limit
        |
        v
100 downstream calls
```

This protects both the dependency and your own connection pool, ThreadPool and memory.

## 7. Inbound rate limiting vs outbound resilience

Inbound:

```text
Internet -> RateLimiter -> Controller
```

protects your API.

Outbound:

```text
User -> HttpClient -> Resilience -> Identity
```

protects your dependency call.

The Framework exposes both capabilities but does not impose a universal quota or policy.

## 8. Fallback

Fallback is a business decision.

Good:

```text
Recommendation Service unavailable
        |
        v
cached recommendations
```

Bad:

```text
Payment Service unavailable
        |
        v
pretend payment succeeded
```

The Framework should provide infrastructure hooks rather than a universal fallback response.

## 9. Retry amplification

Avoid retrying at every layer:

```text
Gateway Retry = 3
Framework Retry = 3
SDK Retry = 3
```

One dependency failure can theoretically become:

`3 x 3 x 3 = 27`

downstream attempts.

Production systems should define clear retry ownership and avoid stacking policies blindly.

## 10. A real learning lab

Use:

```text
Xr.User -> Xr.Identity
```

### Case A: healthy dependency

Identity returns 200.

Observe no retry and no open circuit.

### Case B: slow dependency

Make Identity sleep for five seconds.

Observe:

- attempt timeout;
- total timeout;
- cancellation;
- final exception.

### Case C: 503 dependency

Make Identity return 503 repeatedly.

Observe:

- retry attempts;
- backoff;
- circuit breaker;
- final failure.

### Case D: recovery

Restore Identity to 200 and observe:

`Open -> HalfOpen -> Closed`

### Case E: POST

Simulate:

`POST /orders`

The server commits the DB transaction, but the client loses the response.

Observe why automatic retry can create a duplicate order.

### Case F: idempotency

Add:

`Idempotency-Key: order-create-xxxx`

Store the command/result server-side.

A repeated request should return the first result instead of creating another order.

After these six cases, you understand the semantics rather than merely knowing API names.

## 11. Recommended learning order

1. HttpClientFactory
2. Timeout
3. Retry
4. Idempotency
5. Circuit Breaker
6. Concurrency Limiting
7. Fallback
8. Metrics/Tracing
9. Retry amplification
10. Hedging

Do not start by memorizing Polly internals.

## 12. Framework boundary

The Framework provides:

```csharp
AddFrameworkResilientHttpClient(...)
UseFrameworkResilience()
```

It does not force:

- one retry count for every service;
- one timeout for every dependency;
- automatic retries for every POST;
- a universal fallback;
- Polly-specific public APIs in business code.

The goal is one consistent infrastructure entry point for User, Category, Identity, Trade and future services while preserving business-level policy decisions.
