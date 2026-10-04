# Framework Validation Strategy

## Purpose

Aspros.Basic.Framework is the reusable framework. Xr.User and Xr.Category are consumer projects used to validate whether the framework can support real microservice-style DDD development.

They are not part of the Framework implementation and should not introduce business-specific behavior into the Framework.

## Validation Boundaries

| Capability | Consumer evidence | Framework responsibility |
| --- | --- | --- |
| Domain entities and aggregate roots | User / Category domain models | Entity identity and aggregate lifecycle primitives |
| Value objects | Consumer value-object models | Value equality semantics |
| Repository pattern | Consumer repository contracts and implementations | Repository abstraction and persistence boundary |
| Commands and queries | Application handlers | Application request contracts and dispatch integration |
| Domain events | Category domain-event migration target | Event contract, dispatch abstraction, DI registration |
| Unit of Work | User / Category command handlers | Change tracking, commit boundary, explicit transaction support |
| Integration events | Consumer flows when introduced | Application publisher abstraction and CAP integration boundary |
| Cross-service RPC | Only when a real synchronous consumer call exists | Keep the transport outside Domain; use a consumer-specific port |
| Dependency Injection | Consumer startup configuration | Framework services must work with explicit DI; AutoInject is optional |

## Rules

1. Framework changes must solve reusable framework concerns, not consumer business rules.
2. Consumer projects may expose missing framework capabilities through compilation or runtime failures.
3. A capability is considered reusable only after it can be exercised without knowing the business semantics of a specific consumer.
4. gRPC is introduced at the consumer boundary when a real synchronous cross-service contract exists; the Framework does not ship a speculative generic RPC abstraction.
5. Domain projects must remain independent from ORM, database providers, message brokers, and transport implementations.
6. AutoInject is a convenience layer only. Explicit AddScoped, AddSingleton, AddTransient, and AddMediatR registration remain first-class supported usage.
7. Consumer validation must consume the current Framework build artifacts rather than only previously published Framework packages.

## Consumer Validation Workflow

The Consumer Validation GitHub Actions workflow packs the current Framework projects, checks out the feature/framework-v10-migration branches of Xr.User and Xr.Category, restores both against the local Framework package feed plus NuGet.org, and builds both consumer solutions without modifying either consumer repository.

## Current Framework Test Baseline

The Framework test project covers:

- DDD entity identity semantics.
- Value object equality semantics.
- Aggregate-root domain event collection and clearing.
- IUnitOfWork.CommitAsync persistence-only semantics.
- Explicit DI registration without AutoInject.
- Automatic registration of the new IDomainEventHandler<T> abstraction.
- Automatic MediatR command-handler registration.

Consumer repositories remain validation targets rather than Framework dependencies.