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
6. Domain Event → Integration Event → CAP Outbox can participate in the same transaction boundary.

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

## Domain Events and Outbox

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

Commands involving Domain Events should use `IUnitOfWork.ExecuteInTransactionAsync(...)` so business data, database changes made by Domain Event Handlers, and CAP Outbox records remain inside the same transaction boundary.

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
