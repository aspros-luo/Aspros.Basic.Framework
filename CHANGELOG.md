# Changelog

## 2026-10-04 — Framework Refactor Checkpoint

### Architecture
- Added Aspros.Base.Framework.Abstractions.
- Removed the Domain -> Infrastructure project dependency.
- Kept legacy DI/event fully-qualified type names available through type forwarding.
- Kept ServiceLocator only as a deprecated compatibility shim.

### Persistence
- Refactored UnitOfWork so Register methods stage changes and Commit persists them.
- Added synchronous/asynchronous explicit transactions.
- Preserved external transaction ownership.
- Added tracked-change cleanup after rollback.
- Added lightweight DapperExecutor sharing the current connection/transaction.
- Kept EF Core as the default persistence path.

### Runtime
- Added AddAsprosFramework for one-line registration of common runtime services.
- Improved AutoInject with explicit assembly scanning, TryAdd behavior and loadable-type handling.
- Refactored WorkContext to prefer authenticated claims and use JWT payload parsing only as a Bearer-authenticated compatibility fallback.

### Microservices
- Added framework gRPC server/client registration helpers.
- Added optional Bearer Authorization propagation.
- Added optional gRPC call-context/deadline propagation.
- Added IServiceDiscovery / ServiceEndpoint with Nacos adapter.
- Made permission-service discovery configurable through Options.
- Fixed permission failures so denied requests return 403 and unavailable dependencies return 503.
- Restricted Polly retry/circuit behavior to transient HTTP failures.

### Web API
- Hardened query/body rewriting.
- Prevented duplicate API response envelopes.
- Preserved error ObjectResults instead of rewriting them.

### Utilities
- Hardened paging validation and total-page calculation.
- Hardened string case conversion.
- Made DisposableAction idempotent.
- Validated Snowflake worker/datacenter/sequence ranges.
- Fixed enum display-name resolution.

### Samples and docs
- Added Framework Core gRPC Provider / Consumer sample.
- Added HTTP smoke-test requests.
- Added Chinese and English usage, migration and refactor-checklist documents.
- No GitHub Actions/workflows were added or triggered by this refactor.

### Verification note

The repository was statically reviewed through GitHub after critical changes. Local compilation/integration execution is still required because this environment does not provide a usable .NET SDK runtime, and CI was intentionally not triggered.

## 2026-10-06 — Post-refactor API Audit

### Correctness fixes
- Restored the entity parameter on the generic UnitOfWork RegisterNew/RegisterDirty implementations so they match IUnitOfWork and compile correctly.
- Restored the FrameworkPermissionMiddleware type declaration after the bilingual-comment refactor.
- Kept the compatibility PermissionMiddleware inheritance path working by leaving the framework middleware extensible.
- Added fail-closed handling for Flurl transport failures in permission validation so dependency outages return 503 instead of escaping as 500.

### Verification
- Re-checked the Domain/Infrastructure repository boundary and confirmed BaseRepository remains a Domain-side contract/base query implementation without a direct EF Core dependency.
- Re-checked the Core gRPC Provider/Consumer sample: proto, generated client/server configuration, HTTP/2 provider hosting, HTTP/1 consumer hosting and SmokeTests.http are aligned.
- Re-checked current master after the fixes; no GitHub Actions/workflows were added or triggered.
- Local build/integration execution remains intentionally pending because the current execution environment does not provide a usable .NET SDK, and CI must remain disabled to avoid unwanted workflow notifications.

## 2026-10-06 — Completion Gate Defined

### Final audit decisions
- Removed accidental duplicate permission middleware declaration.
- Preserved compatibility dependencies required by the existing Xr.Category/Xr.User integration instead of removing them as an unsafe package cleanup.
- Added an explicit Newtonsoft.Json dependency because Infrastructure uses it directly.
- Aligned Microsoft.EntityFrameworkCore.Relational with the EF Core 8.0.12 package line.
- Reclassified typed HTTP migration, richer gRPC conventions, ServiceLocator removal, resilience modernization, NuGet/.NET 10 targeting, and broader tests as future evolution rather than blockers for this refactor.

### Completion gate
The framework refactor is considered code-complete only after a real .NET 8 solution build, the Core gRPC Provider/Consumer smoke test, and a minimal regression against the real Xr.User/Xr.Category consumers succeed. Static GitHub review alone is not treated as runtime verification.

## 2026-10-06 - Reverse extraction: EF Core infrastructure

- Added reusable EF Core entity-mapping assembly discovery and `EntityMappingConfiguration<T>`.
- Added composable global QueryFilter support that preserves existing filters.
- Added `AddAsprosDbContext<TContext>` to remove repeated `AddDbContext` + `IDbContext` registration boilerplate.
- Kept business DbContext, entity mappings, tenant models and provider configuration inside consuming services.
- Added bilingual documentation for the extracted EF Core conventions.

## 1.1.6 - Regression and reliability completion gate

- Restored the complete framework-native permission middleware implementation.
- Repaired the disposable Docker regression environment and provider profile.
- Added runtime-oriented Health, RateLimit and Permission regression coverage.
- Added transient HTTP retry coverage and disabled automatic retries for unsafe HTTP methods by default.
- Added Nacos and CAP provider-level regression coverage to the disposable environment.
- Added Polly timeout/circuit-breaker/fallback regression tests and a Permission allow-path regression.
- Added a disposable MySQL migration fixture covering add/script/update/repeat-update plus reviewed RenameColumn data-preservation flow.
- Added a single-entry full-regression runner covering solution build, xUnit, Redis, MySQL, RabbitMQ, Nacos and migration validation.
- Expanded regression coverage for UnitOfWork rollback, WorkContext claims, Paging, String/Enum helpers, ResultModel and DisposableAction.
- Added a real Redis DistributedCache provider regression and isolated provider-only infrastructure variables from the lightweight test profile.
- Kept GitHub Actions/workflows absent by design.
- Bumped Infrastructure and Tools package versions to 1.1.6.

## 1.1.5 - Microservice reliability foundations

- Added opt-in CAP registration tied to the business DbContext for durable messaging patterns.
- Added opt-in standard HTTP resilience registration.
- Added opt-in ASP.NET Core rate limiting registration.
- Added liveness/readiness health-check helpers.
- Added bilingual reliability guidance for distributed transactions, idempotency, locks, degradation and cascading failures.
- Kept all new reliability capabilities outside the default framework registration.
