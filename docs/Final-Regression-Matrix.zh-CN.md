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

最新一次 Docker 执行已成功编译 Infrastructure（41 个警告），随后 IntegrationTests 在编译阶段失败，共 16 个错误。此前的 `IWorkContext` 测试替身、gRPC proto 生成模式和 `Grpc.Core` 命名空间冲突已修复；本轮错误集中在测试代码与依赖 API 的对齐：测试项目缺少 ASP.NET Core Shared Framework 引用，缺少 Distributed Cache / Nacos 扩展命名空间，误用了不存在的 `IHttpClientFactory.GetClient` 和测试 DbContext 的 `ClearTrackedChanges`，并再次遇到 `Grpc.Net` 相对命名空间解析。现已补齐 FrameworkReference 和 using，改为 `CreateClient`、`ChangeTracker.Clear()`，并对 gRPC ClientFactory 使用 `global::` 限定。上述修改尚未经过下一次 Docker 编译确认，xUnit 断言仍未成功执行。Infrastructure 的 RSA 可空性、EF Core 过时 QueryFilter API、QueryString 可空性警告仍待独立处理。

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
| Test Platform | xUnit v3 / MTP v2 / .NET 10 原生 dotnet test 集成 | STATIC VERIFIED；编译及运行仍待确认 |
| Dependency Security | CAP MySQL 的 MySqlConnector 固定到修复版 2.6.2；ASP.NET Core Rate Limiting 使用 FrameworkReference | STATIC VERIFIED；Restore / Build 已通过 |
| Permission | allow / deny 403 / unavailable 503 | BLOCKED Runtime；测试已接线 |
| Nacos | 真实注册 + 健康实例发现 | BLOCKED Runtime；Provider 测试已接线 |
| CAP | EF transaction + RabbitMQ 发布/消费 | BLOCKED Runtime；Provider 测试已接线 |
| Redis | 真实 distributed-cache 写入/读取 | BLOCKED Runtime；测试已接线 |
| Utilities | Paging / String / Enum / ResultModel / DisposableAction | BLOCKED Runtime；测试已接线 |
| WorkContext | 认证 Claims 优先 | BLOCKED Runtime；测试已接线 |
| GitHub Actions | Workflow 文件 | PASS：按约定保持 0 个 |

不能把“源码已经写好”或“测试代码已经存在”标成 PASS。
最新 Docker 执行因限流注册扩展缺少命名空间引用而在 Infrastructure 编译阶段失败；该引用已补齐，仍待重新运行确认。前序 Domain、UnitOfWork 文档注释及 gRPC XML 文档问题已分别修复，但尚未取得完整编译和测试结果。所有 xUnit 与 Provider / Runtime 项目继续保持 BLOCKED。
