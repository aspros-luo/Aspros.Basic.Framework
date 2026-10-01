# Application Pipeline 设计说明

## 目标

Application Pipeline 用于承载应用层横切能力，避免业务 Handler 混入基础设施逻辑。

## 执行流程

```text
Request
  |
  v
Validation Behavior
  |
  v
Transaction Behavior
  |
  v
Handler
  |
  v
Domain Event Dispatch
```

## Pipeline 职责

- 参数验证
- 权限检查
- 日志记录
- 事务控制
- 性能统计
- 领域事件发布

## 设计原则

Application 层负责业务流程编排，Domain 层负责业务规则。

Pipeline 不应该包含具体业务逻辑。
