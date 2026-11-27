# Docker Development Environment

This Compose stack starts local infrastructure and the sample HTTP/gRPC APIs. It is an API-runtime environment, not a Framework test runner. Framework IntegrationTests are run directly on the host with `dotnet test`; the current release results are recorded in `docs/Final-Regression-Matrix.md`.

## Start the complete environment

From the repository root:

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api up -d --build
```

Check container state:

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api ps
```

## Call the APIs

The Consumer exposes HTTP/1.1 on port 7141. It calls the Provider over gRPC/HTTP2 inside the Compose network.

```bash
curl --fail http://localhost:7141/health
curl --fail "http://localhost:7141/core/ping?message=hello-from-api"
curl --fail http://localhost:17041/health
```

Expected behavior:
- Consumer health returns `status: ok`.
- `/core/ping` returns a `pong: hello-from-api` message from the Provider.
- Provider health returns `status: ok`.

The Provider's gRPC endpoint is exposed on `localhost:7041`; its HTTP health endpoint is exposed on `localhost:17041`.

## Infrastructure endpoints

| Service | Host endpoint | Development credentials |
|---|---|---|
| MySQL 8.4 | `localhost:13306` | database `aspros_framework_test`, user `framework`, password `framework_password` |
| Redis 7 | `localhost:16379` | no password |
| RabbitMQ | `localhost:5672` | user `framework`, password `framework_password` |
| RabbitMQ Management | `http://localhost:15672` | user `framework`, password `framework_password` |
| ClickHouse 26.8 LTS | `http://localhost:18123` (HTTP), `localhost:19000` (Native TCP) | user `framework`, password `framework_password` |
| Nacos 2.5.1 | `http://localhost:18848/nacos` | authentication disabled for local development |

These infrastructure containers are started independently from the sample APIs. ClickHouse exposes HTTP on host port 18123 and Native TCP on host port 19000. Starting an infrastructure container does not by itself mean that a business API integration has been validated.

## Logs and shutdown

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api logs -f
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api down
```

The named data volumes are retained after `down`. To also delete the local databases and broker data:

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api down -v
```

## Verification boundary

The old `framework-test`, provider-test, migration-test and full-regression Compose runners are retired and are not part of this environment. Run the current Framework IntegrationTests from the repository root:

```bash
dotnet test --project tests/Aspros.Basic.Framework.IntegrationTests/Aspros.Basic.Framework.IntegrationTests.csproj --configuration Release
```

External-service tests run only when their corresponding test configuration is supplied. The release result and the one known CAP Generic Host test gap are recorded in `docs/Final-Regression-Matrix.md`.

The Migration Fixture is a separate executable fixture, not the IntegrationTests project, and is explicitly excluded from the current release gate. The standalone NuGet package-consumer gate is run separately with:

```bash
bash scripts/verify-packaged-consumer.sh
```

Building the API images compiles the two sample applications and their referenced Framework projects as required for deployment. It does not execute the Framework IntegrationTests project.
