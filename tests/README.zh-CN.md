# Framework 回归测试

这里是 Framework 的独立测试工程。

之前 Framework 重构主要依赖 GitHub 静态审查、Xr.User / Xr.Category / Xr.Identity 真实业务兼容性和人工验证，这不足以作为长期回归保障。

最终矩阵覆盖：

| Area | 目标 |
|---|---|
| Abstractions | Domain 不依赖 Infrastructure |
| DI / AutoInject | 注册、TryAdd、Assembly scanning |
| UnitOfWork | Commit / Rollback / transaction ownership |
| Repository | EF Repository / Dapper executor |
| EF Mapping | 1:N / N:N / Value Object / indexes |
| Migration | add / script / update |
| gRPC | server/client / bearer / call context |
| Service Discovery | Nacos adapter / endpoint |
| Permission | 403 / dependency unavailable |
| HTTP Resilience | timeout / retry / breaker / concurrency |
| Health | liveness / readiness |
| Rate Limit | rejected requests |
| CAP | local transaction + durable message boundary |
| Compatibility | legacy ServiceLocator / permission APIs |
| Serialization | response envelope / Newtonsoft compatibility |

需要真实数据库、MQ、Redis、Nacos 或网络故障的场景，不通过 mock 冒充真实验证，统一使用 dev-environment。

只有真实 build/test 和 Provider 验证完成后才能标记 PASS。没有运行时只能标记未验证。


## 当前可执行回归命令

### 1. 基础测试

```bash
cd dev-environment
docker compose --profile test run --rm framework-test
```

### 2. 完整基础设施

```bash
docker compose --profile infra up -d redis rabbitmq nacos
docker compose up -d mysql
docker compose --profile test run --rm framework-test
```

测试容器会执行：

```text
dotnet restore
dotnet ef --version
dotnet test
```

其中 MySQL 回归测试会读取：

```text
ConnectionStrings__TestDatabase
```

并实际验证：

```text
MySQL -> EF Core DbContext -> Framework UnitOfWork -> Commit
                                       |
                                       +-> DapperExecutor -> same database connection
```

### 3. 当前状态定义

- PASS：已经在真实 Runtime 中执行并通过。
- STATIC VERIFIED：源码/依赖关系已经验证，但尚未执行 Runtime。
- BLOCKED：由于当前执行环境缺少 Docker/.NET 或外部基础设施，无法执行。
- FAIL：已经实际执行并失败。

不要把“测试代码已经写好”标记成 PASS。
