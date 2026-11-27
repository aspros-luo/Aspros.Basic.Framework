# Framework Release Regression Matrix

This is the active evidence matrix for `2.0.0-rc.1`. It supersedes the retired Docker regression-runner instructions. The Compose environment is for local infrastructure and sample API runtime, not an xUnit runner.

## Release gate summary

| Gate | Result | Notes |
|---|---|---|
| Framework solution Release build | PASS | User-confirmed successful Release build. |
| Category API Release build | PASS | User-confirmed successful Release build with project references. |
| IntegrationTests | PASS with one skip | 62 total: 61 passed, 0 failed, 1 skipped. |
| Redis / MySQL / Nacos integration tests | PASS | Executed against local infrastructure. |
| Category API CAP business validation | PASS | Commit, rollback, retry, duplicate delivery and idempotent receipt verified through the real API using RabbitMQ, MySQL and CAP EF Storage. |
| HTTP resilience targeted regression | PASS | Eight targeted tests passed. |
| Standalone Framework Generic Host CAP test | KNOWN GAP | The opt-in test timed out waiting for subscriber delivery. It is not a PASS. |
| Packaged NuGet consumer | PENDING — REQUIRED | Run `bash scripts/verify-packaged-consumer.sh`. |
| Migration regression | EXCLUDED | Explicitly excluded from this release gate; no PASS is claimed. |

## Integration test detail

The user-provided Release run reported:

```text
Total: 62
Failed: 0
Passed: 61
Skipped: 1
```

The only skipped test was `Cap_RabbitMq_CanPublishAndConsumeWithEfStorage`. When explicitly enabled, this standalone Generic Host test timed out while awaiting subscriber delivery. Keep this as a known test-coverage gap.

The Category API CAP validation is a separate and successful real application-path test. It verified one committed business row, zero rolled-back business rows, two retry delivery attempts with one receipt, two duplicate delivery attempts with one receipt, and no receipt for the rolled-back message. CAP provides at-least-once delivery; idempotency is the application's responsibility. This result does not prove every possible Framework Generic Host setup.

## Required next command

```bash
bash scripts/verify-packaged-consumer.sh
```

This packs all six RC NuGet artifacts (five libraries plus the CLI tool), restores the standalone `Framework.Core.Api` from the local package feed with an isolated NuGet cache, builds and starts it, verifies Framework DI / Algorithms / ClickHouse package use, then installs the CLI tool to a temporary tool path and checks its command entry point. Only a successful execution changes the package-consumer gate from PENDING to PASS.

## Environment boundary

Start the local infrastructure and sample APIs only when needed:

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api up -d --build
```

The Compose profiles are not a replacement for the IntegrationTests project. The retired `framework-test`, `provider-test`, `migration-test`, `full-regression` services and old package-consumer runner are intentionally not part of the active Compose environment. Do not use their former commands.

Container health proves only that the process and configured health endpoint respond. API smoke requests prove only the paths actually exercised. Starting MySQL, Redis, RabbitMQ, ClickHouse or Nacos does not by itself verify Framework integration behavior.

## Release decision

Do not promote the RC to `master` or publish stable `2.0.0` until the required packaged-consumer gate is PASS, the known CAP test gap is retained in the release record, and the user explicitly authorizes promotion. Migration remains excluded by the explicit release decision.
