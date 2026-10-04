# Aspros.Basic.Framework

Aspros.Basic.Framework is a lightweight .NET 8 framework for microservice-oriented DDD applications.

## Core
- Domain Kernel: Entity, AggregateRoot, ValueObject, Domain Event, Result, Error
- Application: Command / Query, MediatR Pipeline, Unit of Work, Domain Event Dispatcher, Integration Event Publisher
- Infrastructure: EF Core persistence, CAP integration, ASP.NET Core integration and optional AutoInject

Design target: .NET 10 thinking on a .NET 8 implementation baseline. Keep boundaries clean, use the platform where it already solves a problem, and promote only proven repeated business patterns into framework capabilities.

## Packages
| Package | Responsibility |
| --- | --- |
| Aspros.Base.Framework.Domain | Domain contracts and DDD Kernel |
| Aspros.Base.Framework.Application | Application contracts, MediatR and persistence/message abstractions |
| Aspros.Base.Framework.Infrastructure | EF Core UoW, CAP publisher, ASP.NET Core integration and optional AutoInject |

Current migration package line: **2.0.0**.

## Architecture
```text
Domain
  ↓
Application
  ↓
Infrastructure
  ↓
Database / MQ / Transport
```

Domain does not depend on EF Core, CAP, HTTP, or message brokers.

## Application Pipeline
Framework uses MediatR as the request runtime. It does not maintain a second dispatcher.
```text
Controller / MQ / Job / RPC
        ↓
IMediator / ISender
        ↓
Pipeline Behavior
        ↓
Command / Query Handler
        ↓
Domain
```

Existing consumers may continue to use native MediatR handlers during migration.

## Unit of Work
Simple persistence uses `IUnitOfWork.CommitAsync()`.
Multi-step atomic work uses `ITransactionalUnitOfWork.ExecuteInTransactionAsync(...)`.

`CommitAsync()` is persistence-only. It does not implicitly dispatch Domain Events.

## Domain Event and Integration Event
Domain Events are in-process domain facts. Integration Events are cross-process messages.

```text
Aggregate
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

The Framework does not turn every Domain Event into a broker message automatically.

## gRPC microservice communication

Framework v10 includes optional gRPC infrastructure integration for internal service-to-service calls.

- Server: `AddFrameworkGrpc()`
- Typed client: `AddFrameworkGrpcClient<TClient>(...)`
- Configuration convention: `Grpc:Services:<serviceName>:Address`
- Named gRPC clients are supported.
- Business gRPC contracts remain in consumer/shared Contracts projects; Framework does not own User/Category/Trade protobuf definitions.

Recommended shape:

```text
User / Category / Trade
        ↓
Shared *.proto contracts
        ↓
Generated gRPC clients / services
        ↓
Framework gRPC registration
        ↓
Application business port
```

Use gRPC for synchronous internal calls. Use Integration Event + CAP/Outbox + MQ for asynchronous cross-service consistency.

## Repository
`IRepository<T>` and `BaseRepository<T>` are lightweight repository/query contracts. `BaseRepository<T>` does not own a DbContext or database connection. Concrete persistence implementations stay in consumer Infrastructure.

## DI
AutoInject is optional convenience only. Standard Microsoft DI and `AddMediatR` remain first-class.

## Legacy compatibility
v10 keeps legacy `IAggregateRoot`, `BaseEntity` / `BasicEntity`, legacy event contracts, and legacy Infrastructure-facing UoW members for incremental migration.

The new Kernel `IAggregateRoot` inherits the legacy marker so new aggregates remain compatible with the existing repository constraint.

## Breaking change
The generic root `Aspros.Base.Framework.Domain.Status` type was removed because it collided with business-owned `Status` types.

Use `Aspros.Base.Framework.Domain.ValueObjects.EntityStatus` instead.

This source-breaking change is part of the **2.0.0** package line.

## Documentation
- [Kernel Design](docs/architecture/kernel-design.md)
- [Application Layer](docs/architecture/application-layer-design.md)
- [Application Pipeline](docs/architecture/application-pipeline-design.md)
- [Dependency Rules](docs/architecture/dependency-rules.md)
- [Framework Validation](docs/architecture/framework-validation.md)
- [gRPC Architecture](docs/architecture/grpc.md)
- [v10 Migration Plan](docs/migration/v10-migration-plan.md)
- [Refactor Checklist](docs/refactor-checklist.md)
- [Framework Demo](docs/demo/framework-v10-demo.md)

Chinese documentation is kept in the main `docs` tree; English architecture documents are under `docs/architecture/en`.