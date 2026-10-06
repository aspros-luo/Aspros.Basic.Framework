# 数据库 Code First 与 Migration 路线

## 目标
统一 Framework 的数据库生成路径：

DDD Entity/Aggregate → 显式 EF Mapping → DbContext Model → EF Core Migration → SQL → Database

这里**不实现基于反射的 Entity → SQL 自制生成器**。数据库模型语义由 EF Core 负责，Framework 负责减少业务服务重复配置。

## 当前实现
已经加入：
- `Aspros.Basic.Framework.Tools` 独立 CLI。
- `aspros db migration add`
- `aspros db migration script`
- `aspros db migration update`
- CLI 最终委托给 `dotnet ef`，避免 Framework 自己重新实现 EF Migration 引擎。
- `update` 默认拒绝，必须显式增加 `--allow-update`，用于提醒开发者它会真正修改数据库。
- 生产环境推荐生成 SQL 后审核/部署，而不是应用启动时自动执行 Migration。

## 基本用法
```bash
dotnet tool install --global Aspros.Basic.Framework.Tools

aspros db migration add InitialCreate \
  --project ./Xr.Trade.Infrastructure \
  --startup-project ./Xr.Trade.Api \
  --context TradeDbContext

aspros db migration script \
  --project ./Xr.Trade.Infrastructure \
  --startup-project ./Xr.Trade.Api \
  --context TradeDbContext \
  --idempotent

# 仅开发/测试环境：
aspros db migration update --allow-update \
  --project ./Xr.Trade.Infrastructure \
  --startup-project ./Xr.Trade.Api \
  --context TradeDbContext
```

业务项目仍需要按 EF Core 规范提供 `DbContext`、Provider，以及必要的 design-time 创建方式。

## 为什么 CLI 不自己生成 SQL
Framework 的职责是统一工程体验，而不是复制 EF Core Migration 引擎。

因此：
- Entity / Aggregate 表达业务模型；
- Mapping 表达数据库映射；
- EF Core 计算 Model Diff；
- Migration 记录结构变化；
- Provider 负责 MySQL 等数据库的 SQL 方言；
- Framework CLI 只负责把这些能力串起来。

这样可以天然覆盖：
1. 1:N；
2. N:N；
3. 显式 Join Entity；
4. Value Object / Owned Type；
5. Composite Key；
6. Index / Unique Constraint；
7. Shadow Property；
8. Backing Field；
9. DeleteBehavior；
10. Rename / Type Change 等 Migration 场景。

## 数据库生成测试矩阵
后续必须验证：
- 单实体；
- 1:N；
- N:N 隐式 Join；
- 带业务字段的显式 Join Entity；
- Value Object；
- Composite Key；
- FK 与删除行为；
- Unique / Non-Unique Index；
- Shadow Property / Backing Field；
- Nullable / Required；
- Column Rename；
- 类型、长度、精度修改；
- MySQL / Pomelo SQL；
- Clean DB 创建；
- Existing DB 升级；
- 重复执行 Migration；
- 破坏性变更提示。

## 生产原则
不要把 `Database.Migrate()` 作为多实例微服务的默认启动行为。

推荐：
开发修改 Entity / Mapping → add migration → review → script / bundle → DBA 或部署流程执行

尤其要人工检查：
- Drop Column；
- Drop Table；
- Nullable → Non-Nullable；
- 字符串/Decimal 缩窄；
- Rename 是否真的被识别为 Rename，而不是 Drop + Add；
- 数据迁移是否需要显式 `migrationBuilder.Sql(...)`。

EF Core 可以生成结构迁移，但不能替业务判断“数据如何安全迁移”。

## 当前进度
| 阶段 | 状态 |
|---|---|
| 架构与真实业务调查 | 完成 |
| EF Mapping 提取 | 完成 |
| DbContext 注册 | 完成 |
| CLI Foundation | 完成 |
| Provider / Design-time 验证 | 进行中 |
| Migration 测试矩阵 | 待执行 |
| MySQL / Pomelo 测试 | 待执行 |
| Data-loss 测试 | 待执行 |
| Production SQL / Bundle 流程 | 待执行 |
| 中英文文档 | 进行中 |
