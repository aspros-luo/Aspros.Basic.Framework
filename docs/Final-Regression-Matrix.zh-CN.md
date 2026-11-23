# Framework 2.0.0-rc.1（.NET 10）最终回归验收矩阵

这是 Framework 2.0.0-rc.1 / .NET 10 发布候选的验收入口和状态定义。

状态定义：
- PASS：真实 Runtime 已执行并通过。
- FAIL：真实 Runtime 已执行但失败。
- BLOCKED：当前环境缺少必要 Runtime / 基础设施。
- STATIC VERIFIED：源码、依赖和测试接线已检查，但没有真实 Runtime 结果。

一键验收：
cd dev-environment
docker compose --profile infra --profile full-regression run --build --rm framework-full-regression

一次执行 restore、dotnet-ef、Solution Build、全部 xUnit、MySQL Migration 回归，以及三个 NuGet 包的打包与独立 API 消费者验证；临时环境提供真实 MySQL / Redis / RabbitMQ / Nacos。

基础 test profile 已实际运行过一次：Solution Restore 和 Build 成功，dotnet-ef 输出 10.0.15；随后测试进程在发现测试之前退出，原因是尚未启用 .NET 10 原生 Microsoft Testing Platform Runner。这次没有执行任何 xUnit 断言。

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
| Test Platform | xUnit v3 / MTP v2 / .NET 10 原生 dotnet test 集成 | STATIC VERIFIED；等待重新执行 |
| Dependency Security | CAP MySQL 的 MySqlConnector 固定到修复版 2.6.2；ASP.NET Core Rate Limiting 使用 FrameworkReference | STATIC VERIFIED；Restore / Build 已通过 |
| Permission | allow / deny 403 / unavailable 503 | BLOCKED Runtime；测试已接线 |
| Nacos | 真实注册 + 健康实例发现 | BLOCKED Runtime；Provider 测试已接线 |
| CAP | EF transaction + RabbitMQ 发布/消费 | BLOCKED Runtime；Provider 测试已接线 |
| Redis | 真实 distributed-cache 写入/读取 | BLOCKED Runtime；测试已接线 |
| Utilities | Paging / String / Enum / ResultModel / DisposableAction | BLOCKED Runtime；测试已接线 |
| WorkContext | 认证 Claims 优先 | BLOCKED Runtime；测试已接线 |
| GitHub Actions | Workflow 文件 | PASS：按约定保持 0 个 |

不能把“源码已经写好”或“测试代码已经存在”标成 PASS。
当前环境没有 dotnet / Docker，因此 Runtime 项目前保持 BLOCKED，直到在可运行的 Docker + .NET 10 环境执行一键验收命令。