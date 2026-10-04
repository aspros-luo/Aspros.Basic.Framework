# Application Layer Design

## Goal

The Application Layer orchestrates business use cases without containing core domain rules.

## Layer Relationship

```text
API / MQ / Job / RPC
     |
     v
Command / Query
     |
     v
Application Handler
     |
     +--> Domain Event Handler (when needed)
     |
     +--> Integration Event Publisher (when needed)
     |
     v
Domain
     |
     v
Infrastructure implementations are accessed through Application abstractions
```

The dependency direction remains:

```text
Infrastructure → Application → Domain
```

Application must not directly depend on concrete databases, message brokers, HTTP clients, or other infrastructure implementations.

## Command

A Command represents a business operation that changes system state.

## Query

A Query reads data without changing domain state.

## Handler

A Handler coordinates domain objects, persistence abstractions, and external service abstractions.

## Persistence and Transaction Choice

Not every Command needs an explicit database transaction or a Domain Event.

For simple business:

```text
Controller
   ↓
CommandHandler / Application Service
   ↓
EF Core
   ↓
IUnitOfWork.CommitAsync()
   ↓
Database
```

`CommitAsync()` only persists the changes currently tracked by the Unit of Work. It does not automatically dispatch Domain Events.

For multi-step business that requires all-or-nothing behavior, the handler explicitly uses `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)`.

An explicit transaction scope must be the outermost scope for the current `DbContext`. The Framework intentionally does not emulate nested database transactions: calling `ExecuteInTransactionAsync(...)` while the same `DbContext` already has an active transaction fails immediately with a clear configuration error. Compose the inner work inside the existing transaction instead.

## Domain Events

Application provides the minimal domain-event handling boundary:

- `IDomainEventHandler<TDomainEvent>`
- `IDomainEventDispatcher`

Domain Event Handlers process events raised by aggregates.

They may:

- update other state that must be maintained synchronously;
- call Application-level abstractions;
- publish Integration Events;
- coordinate other use-case capabilities.

They should not introduce concrete CAP, RabbitMQ, or similar infrastructure dependencies into the Domain layer.

## Integration Events

Application uses `IIntegrationEventPublisher` to express the need to publish cross-service messages.

Concrete implementations belong to Infrastructure, such as the current CAP implementation.

```text
Domain Event
      ↓
Application Handler
      ↓
IIntegrationEventPublisher
      ↓
Infrastructure / CAP
```

Domain Events and Integration Events remain separate concepts. Not every Domain Event is automatically sent to a message broker.

When CAP Outbox records must participate in the exact same database transaction, the consuming service supplies the database-provider-specific CAP transaction adapter. The core Framework stays database-provider neutral.

## Unit of Work

Application's `IUnitOfWork` is a lightweight persistence session. It exposes `RegisterNew`, `RegisterRangeNew`, `RegisterDirty`, `RegisterRangeDirty`, `RegisterDeleted`, `RegisterRangeDeleted`, and `CommitAsync`. `Register*` only stages EF Core changes; it does not persist them. A normal use case calls `CommitAsync()` once.

Only the small subset of use cases that genuinely requires local multi-step atomicity should depend on `ITransactionalUnitOfWork` and call `ExecuteInTransactionAsync(...)`.

Inside an explicit transaction, a use case may call `CommitAsync()` once it needs a database-generated key before continuing. In the current implementation this only flushes `SaveChanges` into the still-open transaction; it does not commit the outer transaction. The final commit remains owned by `ExecuteInTransactionAsync(...)`. Domain Event dispatch remains coordinated by the outer transactional path. This should not be treated as a second transaction boundary.

## DI Registration

`AutoInject()` is convenience only. A consumer can register Framework services explicitly with standard Microsoft DI APIs. No Domain or Application capability depends on AutoInject being present.

## Current Strategy

Framework v10 currently provides only the minimum Application foundation:

- Command / Query abstractions
- Handler abstractions
- Optional Pipeline mechanism
- Domain Event Handler / Dispatcher
- Integration Event Publisher

Validation, Idempotency, and additional Pipeline Behaviors should be added only when stable real-world requirements emerge.

The framework does not introduce a complete Application Runtime, validation framework, or message abstraction merely for architectural completeness.

## RPC Boundary

Framework v10 does not define a generic RPC client abstraction because the reviewed real consumers do not currently use one.

When a real synchronous cross-service call appears, prefer a business-specific Application port and implement it in Infrastructure with the actual protocol. For a new internal service-to-service call, gRPC is a suitable default when both sides can share a protobuf contract.

The core Framework does not introduce generic RPC registry, discovery, load-balancing, retry, or serialization abstractions until multiple consumers demonstrate a stable repeated need.

## Pipeline Runtime

The Application Pipeline is backed by the existing MediatR runtime rather than a second Framework dispatcher.

`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`, and `IPipelineBehavior` are directly compatible with MediatR. Business handlers may keep the Framework-level `HandleAsync(...)` method; default interface implementations adapt it to MediatR.

Framework `AutoInject()` explicitly registers discovered open generic Pipeline Behaviors because MediatR 12.1+ no longer scans Behaviors automatically.

## gRPC decision

Use gRPC only for a real synchronous cross-service business contract. The reviewed Xr.User/Xr.Category/Xr.Identity repositories currently have no such business call, so no speculative gRPC client was added.
