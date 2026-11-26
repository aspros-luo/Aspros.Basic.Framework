## 2026-11-26 — Official HTTP resilience migration

- Removed the Framework-owned `PollyExtend` policy factory and its legacy policy regression tests.
- Migrated permission validation from Flurl plus manually executed retry policies to a named `IHttpClientFactory` client using the existing Microsoft standard HTTP resilience pipeline.
- Preserved fail-closed permission semantics: dependency errors, resilience timeouts, open circuits and invalid responses return 503; explicit denial returns 403; caller cancellation propagates.
- Removed direct `Polly` and `Flurl.Http` package references from Infrastructure. The Microsoft resilience package may still bring Polly as an implementation dependency.
- Added HTTP retry/circuit-breaker and permission non-success/timeout/caller-cancellation regression tests. Local validation on 2026-11-26 passed all 8 targeted resilience regressions and the Release build (148 warnings). The dependency tree confirms Polly remains only as a transitive implementation dependency of Microsoft.Extensions.Http.Resilience; direct Polly/Flurl references and legacy source usages are absent. This is not a claim that the full integration suite passed.

## 2026-11-26 — Real multi-user recommendation walkthrough

- Documented a reproducible two-user Category API example using real behavior-event ingestion and recommendation reads.
- Clarified the responsibility boundary: Framework supplies generic ranking/fusion algorithms; each business service owns candidate selection, behavior semantics, scoring policy and the desired balance between personalized and global signals.
- Explained that opposite personalized rankings do not guarantee opposite final RRF order because RRF fuses source ranks and global popularity can remain influential.

## 2026-11-25 — Real Category recommendation read path

- Added `GET /api/categories/recommendations`, which ranks enabled, non-deleted Category candidates using real behavior events already ingested into ClickHouse.
- Deduplicates append-only ClickHouse rows by stable `event_id` before aggregation; calculates user-specific behavior relevance and cross-user popularity over a configurable 1–90 day window.
- Applies transparent baseline weights (view=1, click=3, purchase=8) and a seven-day exponential-decay half-life, then fuses independent personalized/popularity rankings with the Framework's generic RRF algorithm.
- Keeps Category candidate eligibility and business scoring policy in the service; no Category-specific recommendation algorithm was added to Framework. Cold-start behavior falls back to popularity, and a completely empty event window falls back to stable catalog order.
- Documents the real recommendation endpoint separately from the existing synthetic Development validation. Authentication and tenant isolation remain production prerequisites.
- Source changes are committed on `feature/category-service`. The assistant has not run a .NET build or local ClickHouse query; user-side build and runtime verification are required.

## 2026-11-25 — Category behavior events to ClickHouse

- Added `POST /api/categories/{id}/behavior-events` to validate an enabled, non-deleted category and publish a behavior event through the MySQL-backed CAP outbox transaction.
- Added a dedicated CAP subscriber that writes accepted events to ClickHouse using the existing Framework `IClickHouseClient` registration and analytics table; no second ClickHouse client or schema was introduced.
- Added event ID validation and a correlation ID for tracing. ClickHouse remains append-only, so duplicate deliveries are physically possible and analytical queries must deduplicate by the stable business event ID.
- Documented the HTTP smoke test, ClickHouse verification query, and current security boundary: this minimal service has no authentication or tenant isolation, so production callers must supply a trusted authenticated user identity.
- Source and documentation are committed on `feature/category-service`. Build and runtime validation have not been performed by the assistant; local validation is required.

## 2026-11-25 — Generic recommendation ranking foundation

- Added weighted Reciprocal Rank Fusion to `Aspros.Base.Framework.Algorithms`. It fuses ordered candidate lists without requiring their raw scores to share a scale, supports source weights and custom key equality, deduplicates keys within each source, and preserves deterministic tie order.
- Kept candidate recall, tenant/eligibility filtering, feature generation, event semantics and model policy in the consuming business service. No fixed product/merchant/article model or universal scoring weights were introduced.
- Integrated the algorithm into the Category development validation by combining personalized cosine-similarity ranking with global popularity ranking over the ClickHouse validation events.
- Added focused algorithm tests. Build and test execution have not been performed in this change; local validation is still required.

## 2026-11-23 — Docker environment switched to API runtime validation

- Replaced the Docker Compose framework-test, provider-test, migration-test and full-regression runners with two explicit profiles: `infra` for MySQL / Redis / RabbitMQ / Nacos, and `api` for the Core Provider and Consumer services.
- Added a multi-stage API Dockerfile. The API images publish the sample applications for runtime use; they do not execute the Framework xUnit or migration-test projects.
- Split the Provider's HTTP health endpoint and h2c gRPC endpoint, and moved the Consumer sample to plain HTTP so the container workflow does not depend on local development HTTPS certificates.
- Added persistent development volumes, service ports, local credentials, API health checks and direct HTTP smoke requests for Consumer -> Provider.
- Kept Framework test source under `tests/`, but paused its Docker execution path and updated the active documentation. The previous 38/44 result with 4 skipped and 2 failed remains historical and unverified after the later test-only corrections.
- Runtime status: configuration and source changes are committed; Docker Compose startup and API calls still require execution on the user's Docker host. No container or API PASS is claimed by this commit.

## 2026-11-23 — MySQL / CAP regression harness follow-up

- The next Docker run compiled both projects and executed 44 tests: 38 passed, 4 were skipped, and 2 failed. The four skips are expected for Redis, Nacos and RabbitMQ-dependent cases; MySQL is configured in the base test profile and its regression therefore runs.
- Fixed the MySQL UoW/Dapper test to use a per-run unique marker and remove its own row after verification. This prevents the persistent Docker MySQL volume from making repeated runs fail because a previous run left a row with the same fixed name.
- Fixed the CAP publisher registration test to use the relational MySQL EF provider. EF Core InMemory is not a valid provider for CAP's relational EF storage because it does not expose a relational connection string.
- These are test-harness corrections only. They are committed but still require another Docker run; no production framework behavior was changed.
- Current compiler warnings in the reported run: 41 in Infrastructure and 98 in IntegrationTests. They remain separate cleanup work and are not test failures.

## 2026-11-23 — Integration regression test runtime corrections

- The first successful Docker build reached xUnit execution: 33 of 44 tests passed and 11 failed. The failures were reviewed as test-harness/setup and assertion issues rather than treated as 11 production defects.
- Added explicit skip messages to conditional Redis / MySQL / RabbitMQ / Nacos tests, as required by the current xUnit Microsoft Testing Platform runner.
- Registered a test DbContext before resolving the UnitOfWork from `AddAsprosFramework`; the framework registers the UnitOfWork implementation, while the business application remains responsible for supplying `IDbContext`.
- Registered logging in the isolated CAP and HealthChecks service-provider tests.
- Made the rate-limit test explicitly configure HTTP 429; ASP.NET Core's default rejection status is 503 unless the application chooses another status.
- Updated the Polly assertion to expect the typed `BrokenCircuitException<HttpResponseMessage>`.
- Corrected the soft-delete test to verify that the entity is marked Modified and the delete remains staged until commit, rather than expecting an uncommitted in-memory change to alter the persisted query result.
- These test corrections are committed; the next Docker run is required to verify the new runtime result. No production behavior was changed in this pass.
 
## 2026-11-23 — Rate limiter test namespace fix

- Imported `Microsoft.AspNetCore.RateLimiting` in `FrameworkRegressionTests.cs` so the test can resolve `RateLimiterOptions.AddFixedWindowLimiter`.
- The .NET 10 API is present in the ASP.NET Core shared framework; this was a missing test-file namespace import, not a package or framework-reference issue.
- Source fix committed; Docker rebuild and xUnit execution are still pending.

## 2026-11-23 — Integration test API and reference alignment

### Compile fixes
- Added the ASP.NET Core shared-framework reference to the integration-test project so native HTTP result and rate-limiter APIs are available at compile time.
- Imported the distributed-cache extension namespace for `IDistributedCache` string helpers and the Nacos dependency-injection namespace for `AddNacosV2Naming`.
- Replaced the nonexistent `IHttpClientFactory.GetClient` calls with `CreateClient`.
- Qualified gRPC client-factory references with `global::` to avoid the test assembly's `Grpc` child namespace shadowing the root `Grpc` namespace.
- Replaced test-only calls to the nonexistent `ClearTrackedChanges` helper with EF Core's `ChangeTracker.Clear()`.

### Verification state
- The latest Docker run compiled Infrastructure but reported 16 IntegrationTests compile errors. The failures were test-project API/reference mismatches, not 16 independent production defects. The corrections above are committed on the release candidate branch; they have not yet been confirmed by a subsequent Docker build. No xUnit assertions have run successfully yet.

## 2026-11-23 — gRPC test namespace qualification

- Qualified the test service's `ServerCallContext` reference as `global::Grpc.Core.ServerCallContext`. The test namespace itself contains a `Grpc` child namespace, so the unqualified namespace path was resolved relatively as `Aspros.Basic.Framework.IntegrationTests.Grpc.Core` rather than the root `Grpc.Core` namespace.
- This is a C# namespace resolution collision, not a missing gRPC package or missing generated protobuf type.
- The source correction is committed; Docker compilation and xUnit execution remain unverified pending the next run.
## 2026-11-23 — Integration test compile alignment

### Compile fixes
- Updated the three `IWorkContext` test doubles to implement the current interface contract (`GetUserId`, `GetTenantId`, and `Get<T>`). The production interface and `WorkContext` implementation were already aligned; the stale code was isolated to regression-test fakes.
- Changed the regression proto generation from `GrpcServices="Client"` to `GrpcServices="Both"` because the same test assembly hosts a gRPC server implementation and creates typed clients. Client-only generation correctly omitted the server base class required by `RegressionGreeterService`.
- Updated `AddFrameworkGrpc` to call the parameterless `AddGrpc()` overload when no options callback is supplied, avoiding forwarding a nullable delegate to a non-nullable API.

### Verification state
- The latest user Docker run confirms Infrastructure builds successfully in 29.9 seconds. IntegrationTests then failed at compile time with eight errors: six missing interface members across three test doubles, and two diagnostics caused by client-only protobuf generation.
- The above source fixes are committed. They have not yet been confirmed by a subsequent Docker build; xUnit assertions and provider/runtime regressions have still not run.
- Remaining Infrastructure warnings include RSA nullable analysis, EF Core's obsolete query-filter API, and QueryString nullability. The gRPC nullable warning is addressed by the overload selection above; other warnings remain intentionally visible for focused follow-up.
## 2026-11-23 — Rate limiter registration namespace correction

### Compile fix
- Imported `Microsoft.AspNetCore.Builder` in `FrameworkRateLimitingExtensions.cs`, where ASP.NET Core 10 defines the `IServiceCollection.AddRateLimiter` extension.
- Kept the `Microsoft.AspNetCore.App` framework reference; no obsolete standalone RateLimiting package was reintroduced.
- The failure was a missing namespace import, not a missing runtime dependency or a need to replace the native ASP.NET Core rate limiter.

### Verification and warning follow-up
- The user's Docker build reached Infrastructure compilation and failed at the unresolved `AddRateLimiter` extension. This source fix has been committed, but a successful build has not yet been observed.
- The same build reported nullable warnings in the legacy RSA helper, a nullable gRPC options warning, the EF Core obsolete query-filter API warning, and a query-string nullability mismatch. These are not suppressed; they should be reviewed after the build blocker is cleared, with RSA behavior preserved and tested rather than silencing nullable analysis.

## 2026-11-23 — Temporarily disable project-coupled permission compatibility wrapper

### Scope correction
- Disabled the legacy `Aspros.SaaS.System.Infrastructure.PermissionMiddleware` and its `UsePermissionValid` extension in the Infrastructure/Middleware compatibility file.
- The wrapper depended on a consuming-project namespace and attempted to inherit from the framework-native `FrameworkPermissionMiddleware`, which is intentionally sealed. This was an invalid compatibility design, not a reason to unseal the framework middleware.
- The framework-native permission middleware and its opt-in extension remain separate; this change only disables the project-coupled legacy wrapper. Permission runtime tests remain pending and must be interpreted against the framework-native path.

## 2026-11-23 — UnitOfWork documentation syntax and XML comment audit

### Compile fix
- Restored the missing `///` prefix on the English sentence inside `UnitOfWork.cs` XML documentation. The bare sentence was parsed as C# source and produced a cascade of ten syntax diagnostics from one root cause.
- The malformed line was already present in the October 18, 2026 UnitOfWork history and had survived subsequent transaction changes; the current fix does not alter UnitOfWork runtime behavior.
- Closed the `PropagateGrpcCallContext` XML `<summary>` block before its `<param>` documentation in `FrameworkGrpcExtensions.cs`.
- Performed a static XML-summary structure scan across 60 C# files in Abstractions, Domain, Infrastructure, Tools and IntegrationTests. No second prose line that would be parsed as source code was found.

### Verification state
- The latest Docker run reached Infrastructure compilation and failed on the malformed `UnitOfWork.cs` documentation line. The source and related XML documentation have now been corrected.
- No successful solution build or xUnit execution has yet been confirmed after these fixes. Runtime verification remains pending in Docker.
## 2026-11-23 — BasicEntity status declaration correction

### Domain compile fix
- Corrected the `BasicEntity` property declaration to `EntityStatus Status`; the release-candidate enum namespace refactor had accidentally produced the invalid declaration `Status EntityStatus Status`.
- Preserved the existing public property name `Status` and the enum's new `ValueObjects.EntityStatus` type. The existing `DomainStatusRegressionTests` already asserts the intended API (`entity.Status`).
- This compile error was present in the release-candidate preparation commit and is now fixed on the release branch. Runtime build and test execution still require a Docker rerun.
## 2026-11-23 — .NET 10 native test-runner alignment

### Test execution
- Enabled the .NET 10 native dotnet test integration for xUnit v3 / Microsoft Testing Platform v2 through the root global.json runner selection and the test project's MTP runner configuration.
- Set the test project output type to executable, as required by Microsoft Testing Platform.
- Updated the lightweight, provider, and full-regression test commands to the .NET 10 dotnet test --project form.
- Retained Microsoft.NET.Test.Sdk and xunit.runner.visualstudio for backward compatibility with older IDE/test hosts; this change does not attempt a broader test-package cleanup.
- Removed the redundant Microsoft.Extensions.Diagnostics.HealthChecks package reference because the ASP.NET Core shared framework already supplies it.

### Verification state
- The user's latest Docker run confirmed solution restore and build succeeded and dotnet-ef resolved to 10.0.15.
- The test command then stopped before test discovery because the repository had not opted into the .NET 10 native MTP runner. No xUnit assertions have run yet; the regression suite remains unverified pending rerun with this configuration.

## 2026-11-23 — .NET 10 restore and dependency security fix

### Restore correctness
- Replaced the non-existent `Microsoft.AspNetCore.RateLimiting 10.0.15` package reference with the `Microsoft.AspNetCore.App` framework reference. Rate limiting is supplied by the ASP.NET Core shared framework; do not add the retired 7.0 RC-only package or pin it to an older release.
- Pinned the transitive `MySqlConnector` dependency brought by `DotNetCore.CAP.MySql 10.0.2` to patched version `2.6.2` (the minimum patched release for GHSA-473q-m89c-ghf8 is 2.6.1).
- The user's latest Docker run confirmed restore/build now succeed; the remaining test failure was the separate MTP runner configuration documented above.

## 2026-11-23 — Release candidate 2.0.0-rc.1 hardening

### Correctness and API clarity
- Fixed UnitOfWork transaction ownership: a caller-owned transaction must already be active on the same DbContext; CommitAsync only flushes changes into it, and UnitOfWork refuses to roll it back or dispose it.
- Preserved EF Core property-level change tracking for entities loaded by the current DbContext; detached objects remain explicit full-entity updates.
- Added SQLite regression tests for asynchronous and synchronous external-transaction rollback ownership, external-transaction commit ownership and tracked-entity update behavior.
- Moved the framework enum from the root Domain namespace to `ValueObjects.EntityStatus` to avoid collisions with business-owned `Status` types.
- Fixed migration CLI handling of the `--no-build` flag, removed duplicate EF options, and reject command-specific unknown options.

### Dependency and verification hygiene
- Updated .NET 10 / EF Core / ASP.NET package references to 10.0.15, gRPC packages to 2.85.0, HTTP resilience to 10.11.1, and the Oracle MySQL EF provider to 10.0.12.
- Removed direct Infrastructure dependencies that had no implementation usage: SkyAPM, Mapster, AspectCore, MassTransit, MVC Newtonsoft integration, JwtBearer, and the Oracle MySQL EF provider. The Oracle MySQL EF provider remains outside Infrastructure and is referenced directly only by the disposable migration fixture and MySQL integration-test project that configure concrete MySQL DbContexts.
- Made the full Docker regression delegate to one script and included the packed NuGet consumer check in the release gate.
- Added the standard StackExchange Redis registration example without introducing another cache abstraction.

### Release status
- Package version is `2.0.0-rc.1`. Runtime build, integration, migration, gRPC, and external NuGet consumer acceptance remain BLOCKED until run in the prepared Docker environment. This candidate is not yet a stable NuGet release.

## 2026-10-18 — Dependency injection discovery audit

### MediatR registration
- Changed auto-injection discovery to identify assemblies containing MediatR handlers, pipeline behaviors, exception handlers/actions, and processors directly. Previously it inferred assemblies from request types, which could miss handlers located in a separate assembly that defines no request type.
- Kept explicit assembly scanning and the backward-compatible parameterless entry point unchanged.

## 2026-10-18 — UnitOfWork commit semantics audit

### Persistence correctness
- Made `CommitAsync` return `true` after a successful `SaveChangesAsync` even when no tracked rows changed. A no-op commit is successful; the previous non-transactional path incorrectly returned `false` while the explicit-transaction path returned `true` for the same no-op.
- Added a SQLite regression test covering no-op commits with and without the cancellation-token overload.
- Clarified the public `IUnitOfWork.CommitAsync` contract. Runtime execution is still pending in the cloud build environment.

## 2026-10-18 — .NET 10 completion audit

### Test compatibility
- Updated the integration test project to xUnit v3 because its conditional-infrastructure tests use `FactAttribute.SkipUnless` and `SkipType`, which are xUnit v3 APIs.
- Fixed the migration fixture's design-time DbContext factory to use Oracle MySQL provider's `UseMySQL` API instead of the removed Pomelo `UseMySql` / `ServerVersion.AutoDetect` configuration.
- Aligned the test platform and Visual Studio adapter package versions with the xUnit v3 package line.
- Added `dotnet ef --version` to the full-regression Docker Compose command so the executable acceptance path matches the regression matrix.

### Documentation and branch policy
- Added English and Chinese .NET 10 upgrade plans and linked them from README.
- Clarified that source migration is complete on the upgrade branch, while runtime build/test, gRPC, migration, and packaged-consumer acceptance remain pending.
- Kept divergent DDD/business-driven branches separate; no GitHub Actions workflow was added or triggered.

## 2026-10-07 — Packaged Core consumer simulation

### Consumer verification
- Added `samples/Framework.Core.Api` as a standalone .NET 10 Web API.
- The Core API references `Aspros.Base.Framework.Infrastructure` 2.0.0 through NuGet `PackageReference`, not a project reference.
- Added a disposable regression script that packs the three framework packages, restores the Core API from a local package feed, builds it, starts the service and verifies `/health`.
- Added a dedicated Docker Compose profile: `core-package-consumer`.
- Added bilingual consumer documentation.
- Runtime execution remains pending until a machine with .NET 10 SDK / Docker runs the regression script.
## 2026-10-07 — .NET 10 Upgrade

### Platform
- Moved all framework, sample, migration-fixture and integration-test projects from `net8.0` to `net10.0`.
- Bumped framework package versions to 2.0.0 to mark the target-framework breaking change.
- Updated the disposable development image to the .NET 10 SDK and `dotnet-ef 10.0.12`.
- Aligned EF Core / ASP.NET Core package references with the .NET 10 servicing line (10.0.12).
- Switched the migration fixture to Oracle's `MySql.EntityFrameworkCore 10.0.9` provider because the official Pomelo provider had not published a stable EF Core 10 release as of this upgrade.

### Verification note
- GitHub Actions were not triggered.
- Static project/package consistency has been reviewed. Real .NET 10 build, gRPC smoke test and disposable infrastructure regression remain required before the upgrade can be called runtime-verified.

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
The framework refactor is considered runtime-verified only after a real .NET 10 solution build, the Core gRPC Provider/Consumer smoke test, and a minimal regression against the real Xr.User/Xr.Category consumers succeed. Static GitHub review alone is not treated as runtime verification.

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
- Added SQLite transaction regressions for UnitOfWork staging, commit, rollback and Dapper transaction participation.
- Added gRPC bearer propagation regression and Redis cache round-trip regression.
- Added a single full-regression Docker profile covering runtime integrations plus migration validation.
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
