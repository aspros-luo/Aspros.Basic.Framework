# Framework v10 Refactor Checklist

## Completed

| Area | Old problem | Refactored result | Usage |
| --- | --- | --- | --- |
| Entity | Identity semantics were not explicit | Runtime type + Id define equality | Inherit `Kernel.Entity<TId>` |
| ValueObject | Different VO types could collide | Runtime type participates in equality/hash | Inherit `Kernel.ValueObject` |
| AggregateRoot | Event lifecycle was incomplete | Kernel aggregate stores and clears pending events | Inherit `Kernel.AggregateRoot<TId>` |
| Domain Events | New handlers were not consistently registered | `IDomainEventHandler<T>` is supported | Standard DI or optional AutoInject |
| Unit of Work | Commit and transaction behavior were mixed | `CommitAsync` persists only; explicit transaction is opt-in | Use `IUnitOfWork` / `ITransactionalUnitOfWork` |
| CAP | Provider-specific transaction behavior leaked into Framework | Core Framework stays provider-neutral | Consumer Infrastructure supplies CAP adapter |
| Application Runtime | Risk of a second dispatcher | MediatR is the runtime | Use `IMediator` / `ISender` |
| Pipeline | Behavior was not proven executable | Open-generic Behaviors are wired into MediatR | Implement `IPipelineBehavior<,>` |
| Integration Events | Local and cross-service events could be confused | Explicit publisher boundary | `IIntegrationEventPublisher` + CAP/Outbox |
| AutoInject | Could be treated as required | Optional convenience only | Standard DI remains first-class |
| Repository | Possible concern about persistence leakage | Confirmed Framework base is a query contract/convenience base; DB implementation stays in consumer Infrastructure | `ConcreteRepository(IDbContext)` |
| Legacy AggregateRoot | New and old contracts could diverge | New Kernel marker inherits old marker | Incremental migration |
| Legacy Entities | BaseEntity and BasicEntity duplicated history | Both remain compatibility models over common `IAuditableEntity` | Migrate when there is a real benefit |
| API naming | Root `Status` collided with consumer `Status` | `ValueObjects.EntityStatus` | Explicit consumer migration |
| CI | Push + PR caused duplicate runs | Push-triggered build removed; consumer validation manual | Low-noise Actions |
| Tests | Kernel/runtime behavior lacked coverage | Added DDD, transaction, rollback, cascade-event, DI and pipeline tests | Release validation |

## Remaining

### Consumer migration validation
- Migrate legacy consumer `Status` references to `EntityStatus`.
- Validate Xr.User direct database-provider declarations.
- Restore and build Xr.User/Xr.Category against Framework 2.0.0.

### Release closure
- Final review of breaking changes.
- Finalize release notes.
- Create the `v2.0.0` tag/release only after consumer validation is green.

### Deliberately not implemented
- Generic RPC framework without a real cross-service contract.
- Generic gRPC abstraction without a proven consumer requirement.
- Speculative validation, idempotency, logging, metrics, tracing or observability frameworks.
- Replacement Repository abstraction without a demonstrated repeated need.