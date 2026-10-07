# Framework 最终回归验收矩阵

## 验收口径

- **PASS**：真实 Runtime 已执行并通过。
- **STATIC VERIFIED**：源码、依赖和测试链路已验证，但当前没有真实 Runtime 结果。
- **BLOCKED**：当前会话缺少 Docker/.NET 等必要运行环境。
- **FAIL**：已经真实执行并失败。

静态完成不能冒充 Runtime PASS。

## 当前矩阵

| Area | 覆盖内容 | 当前状态 |
|---|---|---|
| DI / AutoInject | 基础注册、Assembly scanning、TryAdd | STATIC VERIFIED |
| UnitOfWork | staging、Commit、Rollback、显式事务 | STATIC VERIFIED；SQLite 回归已接入 |
| EF + Dapper | 共享连接、共享事务 | STATIC VERIFIED；SQLite/MySQL 回归已接入 |
| MySQL | 真实 EF Core + Dapper 持久化 | BLOCKED：当前会话没有 Docker Runtime |
| gRPC | Server/Client RPC、Bearer 透传 | STATIC VERIFIED；TestServer 回归已接入 |
| Nacos | 真实注册、健康实例发现 | BLOCKED：当前会话没有 Docker Runtime |
| Redis | DistributedCache round-trip | BLOCKED：当前会话没有 Docker Runtime |
| RabbitMQ + CAP | DB transaction + durable publish/consume | BLOCKED：当前会话没有 Docker Runtime |
| Permission | allow / deny / unavailable = 200 / 403 / 503 | STATIC VERIFIED；HTTP 回归已接入 |
| HTTP Resilience | transient retry、unsafe method 防重试、timeout、circuit breaker、fallback | STATIC VERIFIED；回归已接入 |
| Health | liveness/readiness HTTP | STATIC VERIFIED；TestServer 回归已接入 |
| Rate Limit | 真实 HTTP 429 | STATIC VERIFIED；TestServer 回归已接入 |
| Migration CLI | add、script、protected update、repeat update | BLOCKED：当前会话没有 Docker/.NET Runtime |
| Migration safety | destructive scaffold 检查、RenameColumn、数据保留 | BLOCKED：当前会话没有 Docker/.NET Runtime |
| Consumer compatibility | Xr.User / Xr.Category / Xr.Identity / Xr.Mahjong 源码兼容性 | STATIC VERIFIED |
| GitHub Actions | workflow | VERIFIED ABSENT |

## 一键完整验收

NaN
NaN
NaN
NaN

该入口一次执行 IntegrationTests、MySQL/Redis/RabbitMQ/Nacos 回归，以及完整 Migration 回归。

## 本版本明确不阻塞的演进项

- 应用启动时自动执行 Migration；
- 完全移除 ServiceLocator；
- 强制所有服务从 HTTP 切换为 gRPC；
- 强制 CQRS / DDD Domain Event；
- 切换到 .NET 10 target framework；
- 创建 GitHub Actions / CI workflow；
- 生产 Deployment Bundle 编排。