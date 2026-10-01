# Framework v10 Migration Plan

## Phase 1：基础边界

- 建立架构基线
- 补充架构文档
- 引入 Kernel 基础类型
- 修正 Domain / Application / Infrastructure 依赖方向

## Phase 2：按实际需求逐步接入 Application 能力

当前不要求一次性完成完整 Application Runtime。

优先顺序：

1. 保留现有 Command / Query / Handler 抽象。
2. 在真实业务出现重复的用例级横切问题后，再实现对应 Pipeline Behavior。
3. 如果出现事务一致性问题，再引入 Transaction 抽象。
4. 如果出现消息重复消费或重复请求，再引入 Idempotency。
5. 如果出现数据库事务与消息发布一致性问题，再引入 Outbox。

## Phase 3：基础设施演进

Infrastructure 继续复用现有技术栈，仅在实际项目中出现明确重复问题时抽象公共能力。

RPC、复杂缓存、完整验证框架、独立 Observability Framework 等不作为当前 Framework v10 的默认组成部分。

## Migration Strategy

Existing projects should be able to migrate gradually without requiring a full rewrite.

核心原则：**增量演进、复用现有能力、真实问题驱动抽象。**
