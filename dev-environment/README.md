# Framework Disposable Development / Test Environment

Provides a reproducible disposable Docker environment with .NET 8 SDK, EF Core CLI 8.0.12 and MySQL 8.4, with optional Redis, RabbitMQ and Nacos infrastructure.

Start:

```bash
cd dev-environment
docker compose up -d mysql
docker compose --profile test run --rm framework-test
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
