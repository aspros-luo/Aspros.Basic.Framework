# .NET 10 Release Candidate and Release Plan

## Release line

- Release candidate: `release/v2.0.0-rc.1`
- Package version: `2.0.0-rc.1`
- Target framework: `net10.0`
- `master` remains unchanged until all release gates are accepted and the user explicitly authorizes promotion.
- Do not add or trigger GitHub Actions as part of this release.

The RC includes the Framework packages, integration infrastructure, generic algorithms / ClickHouse support, the Category reference API, and the HTTP resilience migration.

## Architecture boundaries

- Dependency direction: `Abstractions <- Domain <- Infrastructure`; Domain must not depend on Infrastructure.
- EF Core is the default transactional persistence path. Dapper is an opt-in explicit-SQL path and should share the active connection / transaction when applicable.
- UnitOfWork registration stages changes; `CommitAsync` persists them. Multi-step atomic operations use explicit transactions.
- In-process/domain events are not a durable cross-service message transport. Applications own durable messaging and Outbox decisions.
- gRPC helpers provide transport plumbing; business services own protobuf contracts.
- Optional infrastructure integrations remain opt-in and must not be silently enabled by base registration.
- Keep xUnit v3 and its compatible adapter: environment-conditional tests depend on v3 `SkipUnless` / `SkipType`.

## Current acceptance evidence

| Gate | Status | Evidence / boundary |
|---|---|---|
| Framework solution Release build | PASS | User-confirmed Release build succeeded. |
| Category API Release build | PASS | User-confirmed Release build succeeded with project references. |
| IntegrationTests | PASS with one documented skip | 62 total; 61 passed, 0 failed, 1 skipped. |
| Redis / MySQL / Nacos integration coverage | PASS | The corresponding tests ran in the configured local infrastructure environment. |
| Category API CAP business flow | PASS | Real API flow verified commit, rollback, retry, duplicate delivery and idempotent receipt behavior using RabbitMQ, MySQL and CAP EF Storage. |
| HTTP resilience regression | PASS | Eight targeted retry, circuit-breaker and permission-middleware tests passed. |
| Standalone Framework Generic Host CAP regression | KNOWN GAP | The opt-in test timed out while awaiting subscriber delivery. It is not counted as passed. The Category API business flow above validates its actual application path but does not prove every Generic Host registration shape. |
| Packaged NuGet consumer | PENDING | Run `bash scripts/verify-packaged-consumer.sh`; this gate is required before release approval. |
| Migration regression | EXCLUDED BY RELEASE DECISION | Deliberately excluded from this release gate; this is not a claim that migration behavior passed. |

A PASS means the command or business flow actually ran and succeeded. Static inspection, a Docker container being healthy, or a ProjectReference sample build is not a substitute for the packaged-consumer gate.

## Required packaged-consumer verification

Run from the repository root:

```bash
bash scripts/verify-packaged-consumer.sh
```

The script:
1. Packs all six RC NuGet artifacts: the five libraries (Abstractions, Domain, Infrastructure, Algorithms and ClickHouse) plus the `Aspros.Basic.Framework.Tools` CLI tool.
2. Checks that `Framework.Core.Api` uses `PackageReference`, not `ProjectReference`.
3. Restores using the locally produced packages and nuget.org, with a fresh isolated global package cache.
4. Builds the standalone API.
5. Starts it and verifies `/health`, Framework DI resolution, an Algorithms ranking call and ClickHouse package assembly loading through `/framework`.

If the default local port is occupied, set `PACKAGED_CONSUMER_URL`, for example:

```bash
PACKAGED_CONSUMER_URL=http://127.0.0.1:17211 bash scripts/verify-packaged-consumer.sh
```

The script requires the .NET 10 SDK, curl and access to nuget.org for third-party dependencies. Its temporary package feed, isolated NuGet cache, build outputs and tool installation are removed on exit.

## Release procedure

1. Complete the required packaged-consumer gate and record its actual result.
2. Re-check the RC branch head, working tree / user-local changes, Release builds, IntegrationTests summary and known gaps.
3. Review the final regression matrix and release notes for consistency with the evidence above.
4. Only after explicit user approval, promote the accepted RC to `master` and create the stable `2.0.0` release / package publication. Do not publish the RC packages as stable.
5. Keep the known Generic Host CAP test gap visible in release notes or the release record; do not delete its assertion or relabel its timeout as PASS.

## Compatibility notes

The public `BasicEntity.Status` enum moved to `Aspros.Base.Framework.Domain.ValueObjects.EntityStatus`. Consumers referencing the old root-namespace enum must update that type reference. The .NET 10 target is a breaking package line; consumers should upgrade the core Abstractions, Domain and Infrastructure packages together and use EF Core / ASP.NET Core 10-compatible dependencies. Algorithms and ClickHouse are optional independent packages and can be added when those capabilities are needed.

## Explicitly out of scope

- Migration regression for this release decision.
- Wholesale merge of divergent DDD / application-layer refactors.
- Automatic schema migration at application startup.
- Forced CQRS, Event Sourcing or mandatory domain events.
- Adding or triggering GitHub Actions.
- Merging into `master` or publishing stable packages before the required gate and explicit approval.
