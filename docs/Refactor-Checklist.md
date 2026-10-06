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

## Completion Criteria

The refactor is complete when the required engineering and compatibility gates below are satisfied. Future enhancements are not completion blockers.

### Required

- [x] Domain has no direct Infrastructure dependency.
- [x] Repository, UnitOfWork and DbContext contracts match their implementations.
- [x] UnitOfWork Register/Commit semantics are explicit and transactions remain opt-in.
- [x] A runnable gRPC Provider/Consumer smoke-test sample exists.
- [x] Nacos is exposed through the framework ServiceDiscovery abstraction.
- [x] Permission denial returns 403 and dependency failure returns 503.
- [x] Existing Xr.User / Xr.Category Framework usage has been checked for compatibility.
- [x] Chinese and English documentation describe the current API direction.
- [x] No GitHub Actions/workflows are added or triggered by this refactor.

### Real-environment gates still required

- [ ] Run a full solution build with an available .NET 8 SDK.
- [ ] Start the gRPC Provider/Consumer and execute SmokeTests.http.
- [ ] Run a minimal regression against the real Xr.User / Xr.Category services.
- [ ] Fix only issues exposed by those real builds/tests.

### Future evolution; not a completion blocker

- [ ] Migrate all external HTTP calls to typed/IHttpClientFactory clients.
- [ ] Standardize richer gRPC error codes and metadata.
- [ ] Eventually remove the ServiceLocator compatibility shim.
- [ ] Further unify Polly with newer .NET resilience APIs.
- [ ] NuGet release and .NET 10 multi-targeting.
- [ ] Expand the unit/integration test matrix.

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


## Reverse-extraction audit

- [x] Audited real Framework consumers beyond the core Framework project.
- [x] Extracted reusable EF Core entity-mapping assembly discovery into Infrastructure.
- [x] Extracted reusable global QueryFilter expression composition into Infrastructure.
- [x] Kept business DbContext, entities, Mapping classes and tenant rules inside each business service.
- [x] Added bilingual documentation explaining the extraction boundary and usage.
- [ ] Full .NET build/runtime smoke test remains a local-environment gate because this execution environment does not provide the .NET SDK.
