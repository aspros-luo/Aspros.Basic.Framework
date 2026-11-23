# Final Regression Matrix — 2.0.0-rc.1

Acceptance matrix for release candidate Framework 2.0.0-rc.1 on .NET 10.

Status semantics:
- PASS: executed in a real runtime and passed.
- FAIL: executed in a real runtime and failed.
- BLOCKED: required runtime/infrastructure was unavailable.
- STATIC VERIFIED: source, dependency and test wiring reviewed without runtime execution.

Single-entry acceptance:
cd dev-environment
docker compose --profile infra --profile full-regression run --build --rm framework-full-regression

The command performs restore, dotnet-ef verification, full solution build, the complete xUnit regression suite, MySQL migration regression, and the packed NuGet consumer regression. Disposable infrastructure contains MySQL, Redis, RabbitMQ and Nacos.

The lightweight test profile has progressed through restore, solution build, EF CLI verification (10.0.15), and native MTP test startup. The next attempt exposed a compile error in `BasicEntity.cs` caused by the release-candidate enum rename; the source fix is committed, but no xUnit assertions have run successfully yet.

| Area | Coverage | Current status |
|---|---|---|
| Abstractions | Domain boundary / type forwarding | STATIC VERIFIED |
| DI / AutoInject | registration / TryAdd / assembly scanning | STATIC VERIFIED |
| UnitOfWork | stage / commit / rollback / audit / external transaction ownership | BLOCKED runtime; tests wired |
| EF + Dapper | same database path | BLOCKED runtime; test wired |
| MySQL | EF Core 10 + Oracle MySQL provider | BLOCKED runtime; test wired |
| Migration | add / script / update / repeat update | BLOCKED runtime; disposable fixture wired |
| Migration safety | destructive scaffold / RenameColumn / data preservation | BLOCKED runtime; fixture wired |
| gRPC | real protobuf RPC using TestServer | BLOCKED runtime; test wired |
| Packaged Core API consumer | local NuGet pack -> restore -> build -> startup -> /health | BLOCKED runtime; included in the full-regression script |
| Core Provider/Consumer | solution build + smoke assets | STATIC VERIFIED |
| HTTP Resilience | transient GET retry / unsafe POST protection | BLOCKED runtime; test wired |
| Polly Timeout | overdue operation rejection | BLOCKED runtime; test wired |
| Polly Circuit Breaker | open after transient failures | BLOCKED runtime; test wired |
| Polly Fallback | transient exception -> 503 | BLOCKED runtime; test wired |
| Health | live/readiness HTTP | BLOCKED runtime; test wired |
| Rate Limit | real 429; supplied by the ASP.NET Core shared framework | BLOCKED runtime; test wired |
| Domain BasicEntity | `Status` property typed as `ValueObjects.EntityStatus` | STATIC VERIFIED; compile fix committed, rerun pending |
| Test platform | xUnit v3 / MTP v2 / native .NET 10 dotnet test integration | STATIC VERIFIED; rerun required |
| Dependency security | CAP MySQL MySqlConnector pinned to patched 2.6.2; ASP.NET Core Rate Limiting uses FrameworkReference | STATIC VERIFIED; restore/build passed |
| Permission | allow / deny 403 / unavailable 503 | BLOCKED runtime; test wired |
| Nacos | real registration + healthy discovery | BLOCKED runtime; provider test wired |
| CAP | EF transaction + RabbitMQ publish/consume | BLOCKED runtime; provider test wired |
| Redis | real distributed-cache write/read | BLOCKED runtime; test wired |
| Utilities | Paging / String / Enum / ResultModel / DisposableAction | BLOCKED runtime; test wired |
| WorkContext | authenticated claims preference | BLOCKED runtime; test wired |
| GitHub Actions | workflow files | PASS: none present by design |

Static verification must never be reported as runtime PASS.
The latest Docker attempt exposed and stopped at a Domain compile error before xUnit assertions ran. The malformed `BasicEntity.Status` declaration is now corrected; all xUnit and provider/runtime rows remain BLOCKED until the Docker rerun actually completes them.