# .NET 10 Upgrade and Refactoring Plan

## Decision

Use `feature/framework-net10-upgrade` as the migration baseline. The work branch `feature/framework-net10-completion` is based on the same reviewed commit (`2ff862f14e438c6afb846d18523e4a2a6263008c`), which is 37 commits ahead of `master`.

Do not merge the DDD-hardening, business-driven-refactor, or v10-refactor branches wholesale. They diverge from this baseline and include independent public API and architecture changes. Review their ideas individually and port only changes that are demonstrably compatible and covered by tests.

## Target

- Framework, sample, CLI, integration-test, and migration-fixture projects target `net10.0`.
- Framework packages use version `2.0.0`; the target-framework change is a breaking package line.
- Microsoft EF Core / ASP.NET Core packages are aligned to `10.0.12`.
- CAP packages are aligned to `10.0.2`.
- The MySQL migration fixture uses Oracle `MySql.EntityFrameworkCore 10.0.9`.
- No GitHub Actions workflows are to be added or triggered as part of this work.

## Architecture boundaries to preserve

- `Abstractions <- Domain <- Infrastructure`; Domain must not reference Infrastructure.
- EF Core is the default transactional persistence path. Dapper is an optional path for explicit SQL and should share the active connection/transaction when applicable.
- UnitOfWork registration stages changes; `CommitAsync` persists them. Multi-step atomic operations use explicit transactions.
- In-process/domain events are not a durable integration-message transport. Cross-service delivery requires the application's durable messaging/Outbox strategy.
- gRPC helpers provide transport plumbing; business services own their protobuf contracts.
- Optional infrastructure features remain opt-in and must not be silently enabled by the base registration method.

## Consumer migration

1. Install the .NET 10 SDK and update consumer projects from `net8.0` to `net10.0` when each consumer is ready to take the breaking package line.
2. Update the three framework package references together to `2.0.0`: Domain, Application (where used), and Infrastructure. Confirm the actual package set used by the consumer.
3. Align direct EF Core and ASP.NET Core dependencies with version 10; select a provider that supports EF Core 10. The migration fixture uses Oracle's MySQL provider, which may differ from the provider chosen by an existing production service.
4. Compile each consumer and resolve source/API migration errors explicitly. Do not hide business-side compatibility failures by adding ambiguous framework aliases.
5. Run the framework integration suite, database migration regression, gRPC smoke test, and packaged Core API consumer regression.

## Verification commands

Packaged consumer simulation:

```bash
bash dev-environment/run-core-package-consumer-regression.sh
```

Full disposable integration and migration regression:

```bash
cd dev-environment
docker compose --profile infra --profile full-regression run --rm framework-full-regression
```

The second command requires Docker. Both commands require a working .NET 10 SDK image and package-restore access.

## Acceptance status

Source and project-file inspection is not a runtime PASS. A gate is PASS only after its command has actually executed and succeeded. Until a .NET 10 SDK/Docker runtime is available, builds, integration tests, migrations, gRPC smoke tests, and external-consumer startup remain BLOCKED.

## Out of scope for this upgrade

- Wholesale merge of divergent DDD/application-layer refactors.
- Automatic schema migration at application startup.
- Forced CQRS, Event Sourcing, or mandatory domain events.
- Adding or triggering GitHub Actions.
- Publishing NuGet 2.0.0 before runtime and consumer compatibility gates pass.
