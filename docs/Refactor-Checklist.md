# Refactor Checklist

> This checklist records completed engineering work. It is not the release-acceptance source; use [Final-Regression-Matrix.md](Final-Regression-Matrix.md) for current evidence.

## Completed engineering work

- [x] Separated Abstractions, Domain and Infrastructure; Domain has no direct Infrastructure dependency.
- [x] Kept repository, UnitOfWork and DbContext contracts aligned; Register stages changes and Commit persists them.
- [x] Kept explicit transactions opt-in and ensured rollback clears tracked state.
- [x] Replaced ServiceLocator-based event dispatch with DI and multi-handler dispatch; documented that IEvent is in-process only.
- [x] Improved AutoInject assembly scanning and TryAdd semantics.
- [x] Added gRPC hosting and typed-client registration helpers; business services own protobuf contracts.
- [x] Added Dapper execution support that can reuse the active UnitOfWork connection and transaction.
- [x] Added opt-in service discovery, CAP, HTTP resilience, rate limiting and health-check capabilities.
- [x] Kept optional infrastructure integrations out of the default AddAsprosFramework registration.
- [x] Migrated permission middleware to IHttpClientFactory and the Microsoft standard HTTP resilience pipeline; removed Framework-owned Polly helpers and direct Polly / Flurl references.
- [x] Added Core Provider / Consumer samples and bilingual usage documentation.
- [x] Added Category reference API, generic algorithms and ClickHouse integration to the RC line.
- [x] Preserved xUnit v3 because conditional test skipping relies on v3 SkipUnless / SkipType.

## .NET 10 / 2.0.0-rc.1

- [x] Framework, samples and test projects target net10.0.
- [x] Framework NuGet packages use version 2.0.0-rc.1.
- [x] Microsoft EF Core / ASP.NET Core dependencies are aligned to 10.0.15; CAP packages are aligned to 10.0.2.
- [x] A standalone Core API sample consumes all five Framework packages through NuGet PackageReference.
- [x] Docker Compose is an infrastructure and sample API runtime environment; it is not the Framework xUnit runner.
- [x] Framework solution Release build succeeded.
- [x] Category API Release build succeeded.
- [x] IntegrationTests: 61 passed, 0 failed, 1 skipped (62 total).
- [x] Eight targeted HTTP resilience regressions passed.
- [x] Category API CAP business validation passed for commit, rollback, retry and duplicate/idempotent delivery.
- [ ] Run `bash scripts/verify-packaged-consumer.sh` and record the real result before release approval.
- [ ] The standalone Framework Generic Host CAP test remains a known timeout gap; do not count it as PASS.
- [ ] Migration regression is explicitly excluded from this release gate.

## Design rules

Keep the framework small. Default business flow remains:

    Controller -> Service/Application -> Repository -> DB

Use explicit transactions only when multiple changes must commit atomically. Use durable messaging for cross-service eventual consistency. Do not add CQRS, DDD infrastructure, Event Sourcing, transactions or messaging abstractions merely for architectural completeness.

Do not add or trigger GitHub Actions for this release. Do not promote the RC to master or publish stable 2.0.0 until the required packaged-consumer gate passes and the user explicitly authorizes promotion.
