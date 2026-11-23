# Framework 2.0.0-rc.1（.NET 10）最终回归验收矩阵

这是 Framework 2.0.0-rc.1 / .NET 10 发布候选的验收入口和状态定义。

状态定义：
- PASS：真实 Runtime 已执行并通过。
- FAIL：真实 Runtime 已执行但失败。
- BLOCKED：必要 Runtime / 基础设施不可用，或测试运行器未能启动。
- STATIC VERIFIED：源码、依赖和测试接线已检查，但没有真实 Runtime 结果。

一键验收：
cd dev-environment
docker compose --profile infra --profile full-regression run --build --rm framework-full-regression

一次执行 restore、dotnet-ef、Solution Build、全部 xUnit、MySQL Migration 回归，以及三个 NuGet 包的打包与独立 API 消费者验证；临时环境提供真实 MySQL / Redis / RabbitMQ / Nacos。

最新 Docker 执行已成功编译 Infrastructure 和 IntegrationTests（Infrastructure 41 个警告、IntegrationTests 99 个警告），并真正运行了 44 个 xUnit 测试：33 通过、11 失败。编译阻塞已清除。11 个失败已定位并修正测试接线/断言：5 个条件跳过测试缺少 xUnit 要求的 Skip 消息；CAP 与 HealthChecks 的独立 ServiceCollection 测试未注册 Logging；AddAsprosFramework 的 UoW 测试未提供业务侧必须注册的 IDbContext；限流测试未显式配置 429（ASP.NET Core 默认拒绝码为 503）；Polly 断言未匹配泛型熔断异常；软删除测试错误地期待未提交的逻辑删除改变数据库查询结果。修正仅涉及测试代码，没有改变生产框架行为，现等待下一次 Docker 运行确认。Infrastructure 的 RSA 可空性、EF Core 过时 QueryFilter API、QueryString 可空性警告，以及 IntegrationTests 的 xUnit1051 警告仍待独立处理。

| Area | 覆盖内容 | 当前状态 |
|---|---|---|
| Abstractions | Domain 边界 / Type Forwarding | STATIC VERIFIED |
| DI / AutoInject | 注册 / TryAdd / Assembly Scan | STATIC VERIFIED |
| UnitOfWork | stage / commit / rollback / audit / 外部事务所有权 | BLOCKED Runtime；测试已接线 |
| EF + Dapper | 同数据库链路 | BLOCKED Runtime；测试已接线 |
| MySQL | EF Core 10 + Oracle MySQL Provider | BLOCKED Runtime；测试已接线 |
| Migration | add / script / update / repeat update | BLOCKED Runtime；Docker 夹具已接线 |
| Migration Safety | destructive scaffold / RenameColumn / 数据保留 | BLOCKED Runtime；夹具已接线 |
| gRPC | 真实 protobuf + TestServer RPC | BLOCKED Runtime；测试已接线 |
| Packaged Core API consumer | local NuGet pack -> restore -> build -> startup -> /health | BLOCKED Runtime；已纳入 full-regression 脚本 |
| Core Provider/Consumer | Solution Build + Smoke 资产 | STATIC VERIFIED |
| HTTP Resilience | transient GET retry / unsafe POST protection | BLOCKED Runtime；测试已接线 |
| Polly Timeout | 超时拒绝 | BLOCKED Runtime；测试已接线 |
| Polly Circuit Breaker | 连续故障后熔断 | BLOCKED Runtime；测试已接线 |
| Polly Fallback | transient exception -> 503 | BLOCKED Runtime；测试已接线 |
| Health | live / ready HTTP | BLOCKED Runtime；测试已接线 |
| Rate Limit | 实际 HTTP 429；由 ASP.NET Core Shared Framework 提供 | BLOCKED Runtime；测试已接线 |
| Domain BasicEntity | `Status` 属性使用 `ValueObjects.EntityStatus` 类型 | STATIC VERIFIED；编译修复已提交，等待重新执行 |
| UnitOfWork Source Syntax | XML 文档注释应被识别为注释，而不是 C# 语句 | STATIC VERIFIED；漏写 `///` 已修复，等待重新执行 |
| Test Platform | xUnit v3 / MTP v2 / .NET 10 原生 dotnet test 集成 | PASS：运行器成功执行 44 项测试（33 通过、11 项测试失败，失败项单独跟踪） |
| Dependency Security | CAP MySQL 的 MySqlConnector 固定到修复版 2.6.2；ASP.NET Core Rate Limiting 使用 FrameworkReference | STATIC VERIFIED；Restore / Build 已通过 |
| Permission | allow / deny 403 / unavailable 503 | BLOCKED Runtime；测试已接线 |
| Nacos | 真实注册 + 健康实例发现 | BLOCKED Runtime；Provider 测试已接线 |
| CAP | EF transaction + RabbitMQ 发布/消费 | BLOCKED Runtime；Provider 测试已接线 |
| Redis | 真实 distributed-cache 写入/读取 | BLOCKED Runtime；测试已接线 |
| Utilities | Paging / String / Enum / ResultModel / DisposableAction | BLOCKED Runtime；测试已接线 |
| WorkContext | 认证 Claims 优先 | BLOCKED Runtime；测试已接线 |
| GitHub Actions | Workflow 文件 | PASS：按约定保持 0 个 |

不能把“源码已经写好”或“测试代码已经存在”标成 PASS。
最新 Docker 执行已通过 Infrastructure 与 IntegrationTests 编译，xUnit 执行结果为 33/44 通过、11 失败。上述 11 个测试接线/断言问题已在测试代码中修正，仍待重新运行确认；不得将本轮编译成功等同于回归通过。所有 xUnit 与 Provider / Runtime 项目继续保持 BLOCKED，直到取得真实 PASS 结果。
