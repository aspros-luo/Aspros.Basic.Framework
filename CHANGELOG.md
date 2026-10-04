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
