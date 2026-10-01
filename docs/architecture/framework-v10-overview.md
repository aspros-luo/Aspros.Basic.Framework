# Aspros Basic Framework v10 Overview

## Goal

Framework v10 is an incremental evolution of Aspros Basic Framework toward a modular application foundation.

## Design Goals

- Clear domain boundaries
- DDD-friendly primitives
- Lightweight application abstractions
- Extensible infrastructure integrations
- Keep migration cost controlled
- Prefer real business needs over speculative framework features

## Architecture Direction

The current architecture contains:

- Domain Kernel
- Application Layer
- Infrastructure Abstractions
- Persistence
- Messaging
- Optional cross-cutting Pipeline capabilities

The framework intentionally does not create a heavyweight generic Application Runtime.

## Domain Event and Integration Event

Domain Events are raised by Aggregate Roots and remain independent from infrastructure.

```text
AggregateRoot
     |
     v
Domain Event
     |
     v
Application Domain Event Handler
     |
     +--> local/domain-side effects
     |
     +--> IIntegrationEventPublisher
                    |
                    v
               CAP Outbox
                    |
                    v
               Message Broker
```

Domain Event and Integration Event are deliberately different concepts:

- **Domain Event**: in-process domain semantics.
- **Integration Event**: cross-process or cross-service communication.
- A Domain Event Handler may publish an Integration Event when the business boundary requires it.

When `ExecuteInTransactionAsync(...)` is used, Domain Event handling occurs inside the Unit of Work transaction. With CAP transaction integration enabled, the business data and Outbox record are committed together. CAP documents EF Core transaction integration through `ICapPublisher`.

## Persistence Strategy

Framework v10 does not force a single data-access technology.

### EF Core

EF Core remains the default persistence approach for normal transactional business data.

Typical use cases:

- Aggregate persistence
- Change tracking
- Standard CRUD
- Transactional application workflows
- MySQL relational persistence already used by the framework

### Dapper

Dapper is available in Infrastructure as the lightweight SQL path when EF Core is not the right tool.

Typical use cases:

- Complex SQL
- Reporting-style queries
- Performance-sensitive read paths
- SQL that would become unnecessarily complicated through LINQ
- Existing hand-written SQL that should remain explicit

Dapper does not replace EF Core and is not exposed from the Domain layer.

### ClickHouse

The official ClickHouse .NET driver is available in Infrastructure for analytical workloads.

Typical use cases:

- User behavior/event analysis
- Large-volume analytical queries
- User profiling
- Recommendation and “千人千面” scenarios
- Aggregation and segmentation workloads that should not burden the transactional database

ClickHouse is treated as an analytical data store rather than another transactional ORM.

```text
                    Application
                        |
          +-------------+-------------+
          |                           |
   Transactional Data          Analytical Data
          |                           |
       EF Core                    ClickHouse
          |                           |
        MySQL                Behavior / Profile
                                    |
                             Recommendation
```

Dapper can be used alongside EF Core when a specific SQL path requires it:

```text
Application
    |
    +--> normal persistence --> EF Core --> MySQL
    |
    +--> specialized SQL ----> Dapper --> relational DB
    |
    +--> analytics/profile --> ClickHouse Driver --> ClickHouse
```

The framework intentionally does not introduce a generic “data access” abstraction that hides all three technologies. The concrete choice belongs to the infrastructure implementation required by the application.

## Unit of Work

The lightweight Unit of Work is the application persistence boundary.

For transactional commands:

```text
Command
  ↓
ExecuteInTransactionAsync
  ↓
Domain / Persistence Operations
  ↓
SaveChanges
  ↓
Domain Event Dispatch
  ↓
Handler / Integration Event
  ↓
SaveChanges
  ↓
Commit
```

EF Core supports multiple SaveChanges calls inside an explicit transaction, which allows domain event handlers to participate in the same transaction boundary.

## Migration Principle

Existing capabilities are preserved first. Refactoring happens through incremental migration rather than a destructive rewrite.
