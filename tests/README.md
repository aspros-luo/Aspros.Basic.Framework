# Framework Regression Tests

## Active test entry point

The xUnit v3 IntegrationTests project is executed directly on the host; it is not run by Docker Compose.

From the repository root:

```bash
dotnet test --project tests/Aspros.Basic.Framework.IntegrationTests/Aspros.Basic.Framework.IntegrationTests.csproj --configuration Release
```

Tests that require MySQL, Redis, RabbitMQ or Nacos are environment-gated. Supply the corresponding test configuration only when those services are available. The current release run is recorded in [Final Regression Matrix](../docs/Final-Regression-Matrix.md).

## Retired Compose runners

The former `framework-test`, `framework-provider-test`, `framework-migration-test` and `framework-full-regression` services have been removed from Compose. Do not use their former commands.

## Migration fixture

`Aspros.Basic.Framework.MigrationFixture` is a separate executable fixture, not an xUnit test project. Migration regression is explicitly excluded from the current release decision; this exclusion is not a PASS claim.

## Packaged consumer

The independent NuGet consumer gate is separate from IntegrationTests:

```bash
bash scripts/verify-packaged-consumer.sh
```

See [Final Regression Matrix](../docs/Final-Regression-Matrix.md) for the current release gate status and known gaps.
