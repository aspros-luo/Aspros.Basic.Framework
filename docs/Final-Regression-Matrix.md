# Final Regression Matrix

Acceptance matrix for Framework 2.0.0 on .NET 10.

Status semantics:
- PASS: executed in a real runtime and passed.
- FAIL: executed in a real runtime and failed.
- BLOCKED: required runtime/infrastructure was unavailable.
- STATIC VERIFIED: source, dependency and test wiring reviewed without runtime execution.

Single-entry acceptance:
cd dev-environment
docker compose --profile infra --profile full-regression run --rm framework-full-regression

The command performs restore, dotnet ef verification, full solution build, the complete xUnit regression suite, and MySQL migration regression. Disposable infrastructure contains MySQL, Redis, RabbitMQ and Nacos.

| Area | Coverage | Current status |
|---|---|---|
| Abstractions | Domain boundary / type forwarding | STATIC VERIFIED |
| DI / AutoInject | registration / TryAdd / assembly scanning | STATIC VERIFIED |
| UnitOfWork | stage / commit / rollback / audit | BLOCKED runtime; test wired |
| EF + Dapper | same database path | BLOCKED runtime; test wired |
| MySQL | EF Core 10 + Oracle MySQL provider | BLOCKED runtime; test wired |
| Migration | add / script / update / repeat update | BLOCKED runtime; disposable fixture wired |
| Migration safety | destructive scaffold / RenameColumn / data preservation | BLOCKED runtime; fixture wired |
| gRPC | real protobuf RPC using TestServer | BLOCKED runtime; test wired |
| Core Provider/Consumer | solution build + smoke assets | STATIC VERIFIED |
| HTTP Resilience | transient GET retry / unsafe POST protection | BLOCKED runtime; test wired |
| Polly Timeout | overdue operation rejection | BLOCKED runtime; test wired |
| Polly Circuit Breaker | open after transient failures | BLOCKED runtime; test wired |
| Polly Fallback | transient exception -> 503 | BLOCKED runtime; test wired |
| Health | live/readiness HTTP | BLOCKED runtime; test wired |
| Rate Limit | real 429 | BLOCKED runtime; test wired |
| Permission | allow / deny 403 / unavailable 503 | BLOCKED runtime; test wired |
| Nacos | real registration + healthy discovery | BLOCKED runtime; provider test wired |
| CAP | EF transaction + RabbitMQ publish/consume | BLOCKED runtime; provider test wired |
| Redis | real distributed-cache write/read | BLOCKED runtime; test wired |
| Utilities | Paging / String / Enum / ResultModel / DisposableAction | BLOCKED runtime; test wired |
| WorkContext | authenticated claims preference | BLOCKED runtime; test wired |
| Consumer compatibility | Xr.User / Xr.Category / Xr.Identity / Xr.Mahjong | STATIC VERIFIED |
| GitHub Actions | workflow files | PASS: none present by design |

Static verification must never be reported as runtime PASS.
Current execution environment has no dotnet or Docker, so runtime rows remain BLOCKED until the disposable .NET 10 disposable environment is actually executed.