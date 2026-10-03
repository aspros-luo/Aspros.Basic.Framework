# Framework v10 Kernel Design

## Goal

The Kernel is the lightweight domain foundation of Framework v10.

Design principles:

- No ORM dependency
- No message-broker dependency
- No web-framework dependency
- Keep the Domain layer pure
- Add only capabilities with demonstrated reuse value

## Core Components

### Entity

An Entity is identified by its identity rather than by its property values.

### AggregateRoot

An Aggregate Root defines an aggregate consistency boundary and owns the domain events raised by the aggregate.

The Kernel only maintains pending domain events. It does not dispatch events, publish messages, or commit transactions.

Domain events are processed by Application / Infrastructure as part of the Unit of Work lifecycle when required.

### ValueObject

A Value Object has no independent identity and is compared by its contained values.

### Domain Event

A Domain Event describes a business fact that has occurred.

Examples:

- UserCreated
- OrderPaid
- PaymentCompleted

The Kernel defines the event contract and occurrence time only. It does not depend on a dispatcher, message broker, or transaction implementation.

### Result

Result / Result<T> expresses expected business outcomes without using exceptions for normal business branching.

It does not contain HTTP status-code or API-response semantics.

## Legacy persistence entities

The Framework also keeps two historical persistence base classes:

- `BaseEntity`: legacy audit field naming using `Creator/CreateTime/Updater/UpdateTime/Deleted`;
- `BasicEntity`: newer audit field naming using `Creator/GmtCreated/Modifier/GmtModified/IsDeleted` and Framework lifecycle `EntityStatus`.

Both expose the same semantic audit contract through `IAuditableEntity`, which is what Infrastructure Unit of Work uses for audit stamping.

These types are compatibility/persistence models, not replacements for the Kernel `Entity<TId>` abstraction.

Real consumers currently use both forms, so v10 keeps them side by side.

```text
Legacy consumer aggregate
        ↓
BaseEntity / BasicEntity
        ↓
IAuditableEntity
        ↓
Infrastructure persistence

New DDD aggregate
        ↓
Kernel.Entity<TId> / AggregateRoot<TId>
```

Do not merge these classes merely to reduce the number of base types. A future consolidation should be driven by a demonstrated migration benefit.

## Current Boundary

The Kernel currently contains only:

- Entity
- AggregateRoot
- ValueObject
- Domain Event
- Error
- Result

It intentionally does not contain:

- Specification
- Repository
- Unit of Work
- Domain Service
- Event Bus
- Message Bus
- RPC
- complex cache abstractions

These capabilities belong to higher layers only when real business requirements justify them.

## Evolution Principle

Kernel evolution follows:

> Solve a real repeated problem first, then promote it into a framework capability.

A new abstraction should:

1. address a real business scenario;
2. represent a repeated implementation pattern;
3. reduce long-term maintenance cost;
4. preserve Domain independence from infrastructure.
