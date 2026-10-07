# Regression Acceptance Matrix

## Acceptance rule

- **PASS**: executed in the real runtime and passed.
- **STATIC VERIFIED**: source/dependency/test wiring verified, runtime not executed here.
- **BLOCKED**: execution requires an environment capability unavailable to this session.
- **FAIL**: executed and failed.

Static completion is never reported as runtime PASS.

## Current matrix

| Area | Coverage | Current status |
|---|---|---|
| DI / AutoInject | core registration, assembly scanning, TryAdd | STATIC VERIFIED |
| UnitOfWork | staging, commit, rollback, explicit transaction | STATIC VERIFIED; SQLite regression wired |
| EF + Dapper | shared connection and transaction participation | STATIC VERIFIED; SQLite/MySQL regression wired |
| MySQL | real EF Core + Dapper persistence | BLOCKED: Docker runtime unavailable in current session |
| gRPC | server/client RPC, bearer propagation | STATIC VERIFIED; TestServer regression wired |
| Nacos | real registration + healthy endpoint resolution | BLOCKED: Docker runtime unavailable |
| Redis | distributed cache round-trip | BLOCKED: Docker runtime unavailable |
| RabbitMQ + CAP | DB transaction + durable publish/consume | BLOCKED: Docker runtime unavailable |
| Permission | allow / deny / unavailable = 200 / 403 / 503 | STATIC VERIFIED; HTTP regression wired |
| HTTP Resilience | transient retry, unsafe-method protection, timeout, circuit breaker, fallback | STATIC VERIFIED; regression wired |
| Health | liveness/readiness HTTP endpoints | STATIC VERIFIED; TestServer regression wired |
| Rate Limit | real 429 rejection | STATIC VERIFIED; TestServer regression wired |
| Migration CLI | add, script, protected update, repeat update | BLOCKED: Docker/.NET unavailable |
| Migration safety | destructive scaffold detection + reviewed RenameColumn + data preservation | BLOCKED: Docker/.NET unavailable |
| Consumer compatibility | Xr.User / Xr.Category / Xr.Identity / Xr.Mahjong source review | STATIC VERIFIED |
| GitHub Actions | workflows | VERIFIED ABSENT |

## One-command acceptance

```bash
cd dev-environment
docker compose --profile infra --profile full-regression run --rm framework-full-regression
```

The full profile runs the integration tests, MySQL/Redis/RabbitMQ/Nacos regressions, gRPC/Permission/Resilience/Health/RateLimit tests, and the complete MySQL migration regression.

## Explicit non-goals

- automatic migration at application startup;
- complete ServiceLocator removal;
- forcing every service from HTTP to gRPC;
- mandatory CQRS / DDD domain events;
- .NET 10 target framework change;
- GitHub Actions / CI workflow creation;
- production deployment bundle orchestration.