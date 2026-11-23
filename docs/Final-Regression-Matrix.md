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

The latest user Docker run completed Infrastructure compilation successfully in 29.9 seconds and resolved EF CLI 10.0.15, but IntegrationTests then failed to compile with eight errors. Three test doubles had not implemented the current `IWorkContext` members (`GetTenantId` and `Get<T>`), and the gRPC regression proto was generated as client-only even though the test assembly also hosts the service (missing generated server base type). These test-only mismatches have been corrected; a new Docker run is required to confirm. No xUnit assertions have run successfully yet. Remaining warnings include RSA nullable analysis, EF Core's obsolete query-filter API, and QueryString nullability; the nullable gRPC options warning has been addressed.

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
| UnitOfWork source syntax | XML documentation comments parsed as comments, not C# statements | STATIC VERIFIED; missing `///` fixed, rerun pending |
| Test platform | xUnit v3 / MTP v2 / native .NET 10 dotnet test integration | STATIC VERIFIED; compile/run rerun required |
| Dependency security | CAP MySQL MySqlConnector pinned to patched 2.6.2; ASP.NET Core Rate Limiting uses FrameworkReference | STATIC VERIFIED; restore/build passed |
| Permission | allow / deny 403 / unavailable 503 | BLOCKED runtime; test wired |
| Nacos | real registration + healthy discovery | BLOCKED runtime; provider test wired |
| CAP | EF transaction + RabbitMQ publish/consume | BLOCKED runtime; provider test wired |
| Redis | real distributed-cache write/read | BLOCKED runtime; test wired |
| Utilities | Paging / String / Enum / ResultModel / DisposableAction | BLOCKED runtime; test wired |
| WorkContext | authenticated claims preference | BLOCKED runtime; test wired |
| GitHub Actions | workflow files | PASS: none present by design |

Static verification must never be reported as runtime PASS.
The latest Docker attempt stopped at Infrastructure compilation because the rate-limiter registration extension namespace was missing; the import is now corrected. Previous source syntax and XML documentation errors were also corrected in earlier commits. This is not a substitute for compilation; all xUnit and provider/runtime rows remain BLOCKED until Docker rerun completes.