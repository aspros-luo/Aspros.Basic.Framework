# Framework v10 Migration Plan

## Phase 1: Foundation Boundaries

- Establish the architecture baseline
- Maintain synchronized Chinese and English architecture documentation
- Introduce the Domain Kernel primitives
- Correct Domain / Application / Infrastructure dependency direction

## Phase 2: Incremental Application Capabilities

A complete Application Runtime is not required.

Already implemented:

1. Existing Command / Query / Handler abstractions remain in place.
2. Lightweight Pipeline support remains in place.
3. A lightweight Unit of Work transaction boundary is available.
4. Domain Event Handler / Dispatcher is available.
5. Integration Event Publisher abstraction is available.
6. Domain Event → Integration Event → CAP Outbox is an explicit consumer transaction-adaptation scenario, not a Framework default.

Future capabilities remain driven by real business problems:

- Add Idempotency Behavior when duplicate messages or requests become a real requirement.
- Add additional Pipeline Behaviors only when a stable cross-cutting concern appears.
- Do not create a large runtime or speculative infrastructure abstractions for architectural completeness.

## Phase 3: Infrastructure Evolution

Infrastructure continues to reuse the existing technology stack. Common abstractions should only be promoted when real projects demonstrate a stable repeated need.

Current capabilities:

- EF Core: default transactional persistence;
- Dapper: complex SQL and specialized queries;
- ClickHouse: analytics, user profiles, recommendation, and large-scale aggregation;
- RPC: minimal Application calling contract with concrete implementations in Infrastructure;
- CAP: Integration Event / Outbox implementation.

The framework does not default to introducing:

- RPC registry abstraction
- RPC service-discovery abstraction
- generic data-access abstraction
- independent observability framework
- complete validation runtime

## Legacy Event Compatibility

Existing projects may continue using legacy `IEvent` / `IEventHandler` / `IEventBus` capabilities during migration. They are not forcibly removed in v10 so existing business code can migrate incrementally.

New business code should prefer the v10 `IDomainEvent` / `IDomainEventHandler` / `IDomainEventDispatcher` and `IIntegrationEventPublisher` contracts.

The two event models should not be treated as interchangeable. The legacy event bus must not be assumed to provide reliable cross-service delivery; reliable cross-service communication should use Integration Event + CAP Outbox + Message Broker.

## Domain Events and Outbox

Domain Events do not directly depend on a message broker and are not required for every business operation.

Simple business should prefer `IUnitOfWork.CommitAsync()`. Multi-step business that requires all-or-nothing behavior should explicitly use `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)`.



Domain Events do not directly depend on a message broker.

Recommended flow:

```text
AggregateRoot
    ↓
Domain Event
    ↓
Domain Event Handler
    ↓
IIntegrationEventPublisher
    ↓
CAP Outbox
    ↓
Message Broker
```

Commands involving Domain Events should use `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)` so business data and database changes made by Domain Event Handlers remain inside the same local transaction. If CAP Outbox records must share that transaction, the consumer Infrastructure supplies the database-provider-specific CAP transaction adapter.

## v10 Source-Breaking Changes

`Aspros.Base.Framework.Domain.Status` is removed from the Domain root namespace because a generic Framework type named `Status` conflicts with business-owned `Status` types in real consumers such as Xr.Category.

The generic Framework lifecycle status is now `Aspros.Base.Framework.Domain.ValueObjects.EntityStatus`, and `BasicEntity.Status` uses that type.

Consumers using the old source form `Status = Status.Deleted` or `Status = Status.Normal` must migrate those expressions to an explicit `EntityStatus` reference. This is an intentional v10 source migration; the Framework does not reintroduce the root `Domain.Status` type merely to preserve ambiguous consumer syntax.

## Migration Strategy

Existing projects should be able to migrate gradually without requiring a full rewrite.

Core principle: **incremental evolution, reuse existing capabilities, and let real problems drive abstractions.**


## Application Pipeline Runtime

The lightweight Pipeline is now connected to the existing MediatR runtime.

- `ICommand` / `IQuery` are MediatR requests.
- `ICommandHandler` / `IQueryHandler` remain the Framework-facing handler contracts and adapt to MediatR through default interface implementations.
- `IPipelineBehavior` directly extends MediatR's Pipeline Behavior.
- `AutoInject()` explicitly registers discovered open generic Behaviors.

No separate Application Dispatcher or Runtime has been introduced.

## AutoInject Positioning

`AutoInject()` is a convenience DI registration layer, not a DDD capability.

A consumer can use standard Microsoft DI directly: `AddScoped`, `AddSingleton`, `AddTransient`, and `AddMediatR`.

The Framework's Domain and Application contracts must work without AutoInject.

## Consumer-driven guidance

The current Framework shape is derived from real consumers. Normal commands use a lightweight Unit of Work and one final Commit; explicit transactions are exceptional. Legacy in-process events remain for compatibility, while cross-service reliability uses Integration Event + Outbox + MQ.
