# Framework Disposable Development / Test Environment

Provides a reproducible disposable Docker environment with .NET 10 SDK, EF Core CLI 10.0.12 and MySQL 8.4, with optional Redis, RabbitMQ and Nacos infrastructure.

Start:

```bash
cd dev-environment
docker compose up -d mysql
docker compose --profile test run --rm framework-test
```

Run the MySQL migration regression:

```bash
docker compose --profile migration-test run --rm framework-migration-test
```

Run the single-entry full regression with real MySQL, Redis, RabbitMQ and Nacos:

```bash
docker compose --profile infra --profile full-regression run --rm framework-full-regression
```

This performs restore, EF CLI verification, solution build, the complete xUnit regression suite, and the MySQL migration regression.

For targeted provider regression, run with real MySQL, RabbitMQ and Nacos:

```bash
docker compose --profile infra --profile provider-test run --rm framework-provider-test
```

Enter the test container:

```bash
docker compose run --rm framework-test bash
dotnet build Aspros.Base.Framework.sln
dotnet ef --version
```

Destroy the environment and database:

```bash
docker compose down -v
```

For the full regression environment, start the optional infrastructure profile:

```bash
docker compose --profile infra up -d redis rabbitmq nacos
docker compose --profile test run --rm framework-test
```

This is not a production deployment configuration and does not create GitHub Actions. It is the reproducible environment for MySQL / Redis / RabbitMQ / Nacos / gRPC / CAP regression.


## Packaged Core consumer

Run the external-package simulation without GitHub Actions:

    docker compose --profile core-package-consumer run --rm framework-core-package-consumer

The command packs the framework projects, restores `samples/Framework.Core.Api` from the temporary local NuGet feed, builds it, starts the API and verifies `GET /health`.
