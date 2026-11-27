# .NET 10 正式发布候选与发布计划

## 发布分支与版本

- 发布候选分支：`release/v2.0.0-rc.1`
- NuGet 候选版本：`2.0.0-rc.1`
- 目标框架：`net10.0`
- 所有发布门槛通过并获得用户明确授权前，`master` 保持不变。
- 本次发布工作不新增、不触发 GitHub Actions。

当前 RC 包含 Framework 核心包、集成基础设施、通用算法 / ClickHouse 能力、Category 参考 API，以及 HTTP resilience 改造。

## 必须保持的架构边界

- 依赖方向为 `Abstractions <- Domain <- Infrastructure`；Domain 不得依赖 Infrastructure。
- EF Core 是默认事务持久化路径；Dapper 是显式 SQL 场景下的可选路径，适用时应复用当前连接与事务。
- UnitOfWork 注册操作只暂存变更；`CommitAsync` 才负责持久化。多步骤原子操作显式使用事务。
- 进程内 / 领域事件不等于可靠跨服务消息传输；持久化消息和 Outbox 策略由业务应用负责选择。
- gRPC 辅助能力只负责传输接线；protobuf 契约归业务服务所有。
- 可选基础设施集成保持显式启用，不由基础注册方法隐式强制开启。
- 保留 xUnit v3 及兼容 Adapter：环境条件测试依赖 v3 的 `SkipUnless` / `SkipType`。

## 当前验收证据

| 验收项 | 状态 | 证据与边界 |
|---|---|---|
| Framework Solution Release 构建 | PASS | 用户已确认 Release 构建成功。 |
| Category API Release 构建 | PASS | 用户已确认 API 及 ProjectReference 依赖构建成功。 |
| IntegrationTests | PASS，另有一项明确跳过 | 共 62 项：61 通过、0 失败、1 跳过。 |
| Redis / MySQL / Nacos 集成覆盖 | PASS | 已在配置好的本地基础设施环境中实际执行对应测试。 |
| Category API CAP 业务链路 | PASS | 真实 API 链路验证了事务提交、事务回滚、消息重试、重复投递和幂等收据，使用 RabbitMQ、MySQL 与 CAP EF Storage。 |
| HTTP resilience 回归 | PASS | 8 项重试、熔断和权限中间件针对性测试通过。 |
| Framework 独立 Generic Host CAP 回归 | 已知缺口 | 显式启用后等待 Subscriber 投递超时，不计为通过。上面的 Category API 业务验证覆盖实际应用路径，但不能证明所有 Generic Host 注册方式均正常。 |
| 独立 NuGet 消费者验证 | 待执行 | 发布前必须执行 `bash scripts/verify-packaged-consumer.sh`。 |
| Migration 回归 | 按发布决策排除 | 本次明确不作为发布门槛；这不代表 Migration 已通过。 |

只有真实执行并成功的命令或业务链路才可以标记为 PASS。静态检查、Docker 容器健康，以及使用 ProjectReference 的示例构建，都不能替代独立 NuGet 包消费验证。

## 必须完成的独立 NuGet 消费验证

在仓库根目录执行：

```bash
bash scripts/verify-packaged-consumer.sh
```

脚本会依次：
1. 以 Release 配置打包全部六个 RC NuGet 产物：五个类库（Abstractions、Domain、Infrastructure、Algorithms、ClickHouse）以及 `Aspros.Basic.Framework.Tools` CLI 工具包。
2. 检查 `Framework.Core.Api` 使用的是 `PackageReference`，而不是 `ProjectReference`。
3. 使用本地刚生成的包和 nuget.org 还原依赖，并使用全新的隔离 NuGet 全局缓存。
4. 编译独立 API 消费者。
5. 启动 API，检查 `/health` 和 `/framework`，验证 Framework DI、Algorithms 排序算法调用以及 ClickHouse 包程序集加载。
6. 将 CLI 工具包安装到临时 tool path，并验证命令入口能输出预期帮助信息。

如果默认本地端口被占用，可以指定：

```bash
PACKAGED_CONSUMER_URL=http://127.0.0.1:17211 bash scripts/verify-packaged-consumer.sh
```

需要 .NET 10 SDK、curl，以及访问 nuget.org 下载第三方依赖的网络。脚本退出时会清理临时包目录、隔离 NuGet 缓存、构建输出和临时工具安装目录。

## 发布流程

1. 完成独立 NuGet 消费者验证，并记录真实执行结果。
2. 再次核对 RC 分支 HEAD、用户本地工作区 / 未提交修改、Release 构建、IntegrationTests 摘要和已知缺口。
3. 确保最终回归矩阵与发布说明中的验收状态一致。
4. 只有获得用户明确授权后，才将已验收 RC 提升到 `master`，并创建稳定版 `2.0.0` 发布 / 包发布；不得把 RC 包冒充稳定包发布。
5. 在发布记录或说明中保留 Generic Host CAP 测试缺口；不得删除断言或把超时改写成 PASS。

## 兼容性说明

公共类型 `BasicEntity.Status` 的枚举已迁移到 `Aspros.Base.Framework.Domain.ValueObjects.EntityStatus`。引用旧根命名空间枚举的消费者需要调整类型引用。.NET 10 是破坏性升级版本线；消费者应将核心 Abstractions、Domain、Infrastructure 三个包成组升级，并使用兼容 EF Core / ASP.NET Core 10 的依赖。Algorithms 和 ClickHouse 是独立可选包，按需引用。

## 明确排除项

- 本次发布决策排除 Migration 回归。
- 不整体合并分叉的 DDD / Application Layer 重构。
- 不在应用启动时自动执行数据库迁移。
- 不强制引入 CQRS、Event Sourcing 或领域事件。
- 不新增、不触发 GitHub Actions。
- 未完成必需验证并获得明确授权前，不合并到 `master`，也不发布稳定包。
