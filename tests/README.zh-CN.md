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
