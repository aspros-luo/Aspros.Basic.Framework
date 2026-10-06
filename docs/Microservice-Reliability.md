# Microservice Reliability

This document defines the Framework reliability boundary: reusable primitives are provided, but policies remain opt-in. It covers rate limiting, HTTP timeout/retry/circuit breaker/concurrency control, health/readiness, CAP durable messaging, idempotency, distributed locking, cascading-failure protection, and why the base Framework does not implement cross-service 2PC or universal Saga orchestration.

## Core rule

    Controller -> Service/Application -> Repository -> DB

Reliability features are enabled only when a service needs them.

## Distributed transaction

Prefer local database transaction + durable message + eventual consistency:

    Order DB transaction
        |
        +-- Order state
        +-- CAP local message record
                    |
                    v
                 RabbitMQ
                    |
                    +--> Inventory
                    +--> Fund
                    +--> Recommendation

CAP closes the business-data/message consistency gap. It does not create a cross-service ACID transaction. Cross-service workflows still need idempotency, explicit state transitions and, when necessary, Saga/compensation.

    builder.Services.AddFrameworkCap<OrderDbContext>(options =>
    {
        options.UseRabbitMQ(configuration["RabbitMQ:ConnectionString"]!);
    });

## HTTP resilience

    builder.Services.AddFrameworkResilientHttpClient(
        "permission",
        client => client.BaseAddress = new Uri("https://permission"));

Or:

    builder.Services.AddHttpClient("trade").UseFrameworkResilience();

Retry must only be used for operations that are safe to repeat or protected by idempotency. Do not blindly retry payment submission, order creation or inventory deduction. Timeout, circuit breaker and concurrency limiting protect resources; fallback is a business decision and must not turn failed writes into success.

## Inbound rate limiting

ASP.NET Core supports fixed-window, sliding-window, token-bucket and concurrency limiters. The Framework exposes registration but does not choose a universal quota.

    builder.Services.AddFrameworkRateLimiting(options =>
    {
        options.AddFixedWindowLimiter("api", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromSeconds(1);
        });
    });

    app.UseRateLimiter();

Gateway/WAF and service-level limits should be treated as defense in depth.

## Health and Kubernetes

    builder.Services.AddFrameworkHealthChecks();
    app.MapFrameworkHealthChecks();

Liveness answers whether the process is alive. Readiness answers whether traffic should be sent to the instance. Dependency checks that determine readiness should be tagged `ready`; liveness should remain lightweight.

## Idempotency

MQ redelivery, HTTP retries and repeated client commands require an idempotency boundary:

    request/message -> idempotency check -> business operation -> durable state

The Framework deliberately does not force Redis. The record may live in the business database, Redis, CAP state or another store. For payment, inventory, order and accounting commands, idempotency is business correctness.

## Messaging

CAP/MQ should provide durable publishing, retry, consumer groups, delayed delivery where supported, failure visibility and eventual consistency. Consumers still need idempotency, cancellation support, bounded concurrency and explicit retryable/non-retryable error handling.

`IEventBus` remains an in-process event mechanism and is not a replacement for durable MQ.

## Distributed lock

A future lock abstraction may support singleton jobs, duplicate-settlement protection and maintenance tasks. It must not replace database constraints or idempotency, and Redis should remain optional.

## Cascading failure

Consider the complete chain:

    Timeout -> Retry -> Circuit Breaker -> Concurrency Limit -> Fallback -> Health/Readiness

Do not stack policies blindly. Retry amplification can turn one dependency outage into a cluster-wide failure.

## Intentionally outside the base Framework

The Framework does not force a single MQ vendor, lock implementation, global quota, universal fallback response, Saga for every service, cross-database 2PC, mandatory Redis, mandatory CAP, or mandatory CQRS/DDD.

The goal is to provide standard infrastructure entry points while leaving business policy to each microservice.
