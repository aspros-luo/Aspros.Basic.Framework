# Application Pipeline Design

## Goal

The Application Pipeline hosts cross-cutting capabilities at the **Application use-case execution layer**, keeping business handlers independent from infrastructure implementations.

It is different from ASP.NET Core Middleware:

- Middleware owns HTTP / transport lifecycle.
- Application Pipeline owns Command / Query use-case lifecycle.

## Execution Flow

```text
HTTP / MQ / Job / RPC
        |
        v
Command / Query
        |
        v
Application Pipeline
        |
        +--> Transaction (when needed)
        |
        +--> Idempotency (when needed)
        |
        +--> Domain Event (inside transaction when needed)
        |
        +--> Integration Event / Outbox (triggered by handlers when needed)
        |
        v
Handler
        |
        v
Domain
```

## Transaction

Transaction is a useful framework abstraction because it represents the consistency boundary of an Application Command.

The framework exposes normal persistence through `IUnitOfWork` and keeps explicit local transactions in the optional `ITransactionalUnitOfWork` capability.

On success, Infrastructure performs persistence, domain-event dispatch, and the final commit. On failure, the transaction is rolled back.

## Domain Events

Domain Events are raised by Aggregate Roots when meaningful domain state changes occur.

The Domain layer only owns:

- `IDomainEvent`;
- raising events from Aggregate Roots;
- keeping pending events.

The Domain layer does not depend on:

- MediatR;
- CAP;
- RabbitMQ;
- Kafka;
- HTTP;
- other infrastructure technologies.

The Application layer provides:

- `IDomainEventHandler<TDomainEvent>`
- `IDomainEventDispatcher`

Infrastructure provides the default DI-based dispatcher.

### Transactional Processing Order

```text
Command Handler
      ↓
IUnitOfWork.ExecuteInTransactionAsync(...)
      ↓
Domain / Persistence Operations
      ↓
EF Core SaveChanges
      ↓
Dispatch Domain Events
      ↓
Domain Event Handler
      ↓
IIntegrationEventPublisher (optional)
      ↓
SaveChanges again
      ↓
Commit
```

Domain events are processed inside the explicit Unit of Work transaction. Database changes made by a domain-event handler therefore remain inside the same transaction.

If a handler publishes an Integration Event and CAP transaction integration is enabled, business data and the CAP Outbox record are committed or rolled back together.

### Domain Event vs Integration Event

The framework deliberately keeps the two concepts separate:

- **Domain Event**: in-process domain semantics.
- **Integration Event**: cross-process or cross-service communication.
- A Domain Event Handler may publish an Integration Event when the business boundary requires it; not every Domain Event must enter a message broker.

### Cascading Domain Events

A Domain Event Handler may raise another Domain Event.

Infrastructure continues processing pending events until the aggregate roots have no undispatched events:

```text
Event A
  ↓
Handler A
  ↓
Event B
  ↓
Handler B
```

All of these operations remain inside the current Unit of Work transaction.

## Outbox

The framework already uses DotNetCore.CAP for the Outbox/integration-event path.

The recommended flow is:

```text
Aggregate
   ↓
Domain Event
   ↓
Domain Event Handler
   ↓
IIntegrationEventPublisher
   ↓
CAP
   ↓
Outbox
   ↓
Message Broker
```

Application depends only on `IIntegrationEventPublisher`, not on CAP directly.

## Idempotency

Message consumers, payment callbacks, and retried requests may execute the same Command more than once.

Idempotency belongs to the use-case execution layer and can later be implemented as a Pipeline Behavior. Its storage mechanism should not be placed in the Application abstractions.

## Unit of Work

Framework v10 keeps a lightweight Unit of Work to represent the persistence boundary of an application use case.

Current contract:

- `RegisterNew` / `RegisterRangeNew`: stage inserts;
- `RegisterDirty` / `RegisterRangeDirty`: stage updates;
- `RegisterDeleted` / `RegisterRangeDeleted`: stage deletes or soft deletes;
- `CommitAsync()`: persist staged changes;
- `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)`: opt in to an explicit local transaction.

A command should use `ITransactionalUnitOfWork` only when its local business semantics require atomic multi-step persistence. Domain Events and Integration Events remain optional.

The Unit of Work does not:

- define repositories;
- expose DbContext;
- manage database connections;
- leak EF Core types into Application;
- define a concrete database transaction type in Application.

The framework still does not introduce:

- `ITransaction`
- `ITransactionManager`
- `TransactionScope` abstraction
- custom database transaction interfaces

This provides a usable Domain Event → Integration Event → Outbox path without wrapping EF Core and CAP in another redundant transaction model.


## Runtime Implementation

The framework does not maintain a second request dispatcher.

The existing `ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`, and `IPipelineBehavior` contracts are directly wired into MediatR:

```text
HTTP / MQ / Job / RPC
        |
        v
Command / Query
        |
        v
MediatR
        |
        +--> IPipelineBehavior
        |
        v
Handler
        |
        v
Domain
```

Business handlers continue to expose `HandleAsync(...)`. Default interface implementations adapt them to MediatR's `Handle(...)`, so business code does not need a second handler method.

MediatR 12.1+ no longer automatically scans Pipeline Behaviors, so Framework `AutoInject()` explicitly discovers open generic Behaviors and registers them in deterministic type-name order. This keeps the existing auto-injection model while making the Pipeline actually executable.

### Handler Contracts

- `ICommand`: state-changing request;
- `ICommand<TResult>`: state-changing request with a response;
- `IQuery<TResult>`: read-only request;
- `ICommandHandler<TCommand>` / `ICommandHandler<TCommand, TResult>`: Command handlers;
- `IQueryHandler<TQuery, TResult>`: Query handlers.

Commands and Queries are MediatR Requests and can therefore enter the Pipeline through `IMediator` / `ISender`.

### Pipeline Behavior

Framework `IPipelineBehavior<TRequest, TResponse>` directly extends MediatR's Pipeline Behavior. The framework does not introduce another execution engine.

Only concrete Behaviors required by real application scenarios should be added. Logging, validation, caching, idempotency, and transaction behaviors are not enabled speculatively.

MediatR requires explicit Behavior registration from 12.1 onward; Framework handles that registration centrally.

## End-to-end validation

Framework-level tests now verify that the existing Application Pipeline is executable through MediatR rather than being only a contract.

The validation covers three compatible handler styles used by real consumers:

- Framework `ICommandHandler<TCommand, TResult>`
- Framework `IQueryHandler<TQuery, TResult>`
- direct MediatR `IRequestHandler<TRequest, TResult>`

The same registered open-generic `IPipelineBehavior<TRequest, TResponse>` runs around each handler in the expected order:

```text
MediatR Send
    ↓
Behavior (before)
    ↓
Handler
    ↓
Behavior (after)
```

This is intentionally a wiring/runtime verification only. Framework does not force consumers to migrate existing MediatR handlers before they have a reason to do so.

## Consumer-driven guidance

The current Framework shape is derived from real consumers. Normal commands use a lightweight Unit of Work and one final Commit; explicit transactions are exceptional. Legacy in-process events remain for compatibility, while cross-service reliability uses Integration Event + Outbox + MQ.
