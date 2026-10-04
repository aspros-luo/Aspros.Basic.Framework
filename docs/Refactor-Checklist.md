# Refactor Checklist

## Completed

- [x] Removed the Domain -> Infrastructure project dependency.
- [x] Added an Abstractions project for DI markers and in-process event contracts.
- [x] Added type forwarding for legacy Infrastructure-qualified marker/event types.
- [x] Changed the repository base to depend on the Domain-side IEntitySetProvider abstraction.
- [x] Kept existing EF repository constructors source-compatible through IDbContext implementing IEntitySetProvider.
- [x] Made UnitOfWork stage changes and persist them only from CommitAsync.
- [x] Made explicit transactions opt-in.
- [x] Stopped UnitOfWork from disposing EF Core's DbConnection.
- [x] Made rollback clear and dispose the current transaction.
- [x] Fixed soft-delete audit handling for batch operations.
- [x] Replaced ServiceLocator-based event dispatch with DI and multi-handler dispatch.
- [x] Explicitly documented that IEvent is in-process only, not a durable integration event.
- [x] Improved AutoInject with explicit assembly scanning and TryAdd semantics.
- [x] Added ReflectionTypeLoadException handling to assembly scanning.
- [x] Added framework-level gRPC hosting and typed client registration.
- [x] Added optional Bearer Authorization forwarding for outgoing gRPC calls.
- [x] Added a lightweight Dapper executor that can reuse the current UnitOfWork transaction.
- [x] Made WorkContext claim-first and removed its Redis cache dependency.
- [x] Fixed permission middleware so failed permission checks do not pass through.
- [x] Restricted Polly retry/circuit-breaker decisions to transient failures.
- [x] Added Core Provider / Consumer gRPC samples.
- [x] Added HTTP smoke tests.
- [x] Added Chinese and English documentation.

## Next

- [ ] Consolidate external HTTP calls around IHttpClientFactory / typed clients.
- [ ] Abstract service discovery so HTTP and gRPC can share Nacos-based resolution.
- [ ] Add a framework-level gRPC error-code / deadline convention.
- [ ] Review the Web API result wrapper to eliminate possible double-envelope responses.
- [ ] Migrate remaining ServiceLocator usage in business applications and eventually remove it.
- [ ] Decide whether Microsoft.Extensions.Http.Resilience should replace the remaining legacy Polly APIs.
- [ ] Add core unit tests and a minimal integration test suite.
- [ ] Re-validate Xr.User, Xr.Category and Xr.Identity against the refactored framework.
- [ ] Only after compatibility is stable, consider package versioning and .NET 10 multi-targeting.

## Design rule

Keep the framework small.

Default business flow:

    Controller -> Service/Application -> Repository -> DB

Use explicit transactions only for business operations that need multiple changes to commit atomically.

Use durable messaging for cross-service eventual consistency.

Do not add CQRS, DDD infrastructure, Event Sourcing, transactions, or messaging abstractions merely for architectural completeness.

- [x] Added IServiceDiscovery / ServiceEndpoint with Nacos as the default adapter.
- [x] Permission validation now resolves the permission service through the discovery abstraction.
- [x] Permission service name, group and validation path are configurable options.

- [x] Added AddAsprosFramework for one-line registration of common runtime services.

- [x] Hardened pagination for invalid page numbers/page sizes and zero totals.
- [x] Hardened string conversion helpers for null and empty inputs.
- [x] Made DisposableAction idempotent.
- [x] Added range validation to Snowflake worker/datacenter/sequence inputs.
- [x] Prevented duplicate successful API envelopes and preserved error results.
- [x] Added a framework-native permission middleware entry point.

- [x] Added RollbackAsync and aligned sync/async rollback behavior.
- [x] Rollback clears EF ChangeTracker state to prevent accidental persistence after rollback.
- [x] Moved permission middleware implementation into the framework namespace; the legacy SaaS type is now only a compatibility wrapper.
- [x] Added opt-in gRPC deadline/cancellation context propagation.
