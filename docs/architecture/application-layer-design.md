# Application Layer 设计说明

## 目标

Application Layer 负责业务流程编排，不包含核心业务规则。

## 分层关系

```
API
 |
 v
Command / Query
 |
 v
Handler
 |
 v
Domain
 |
 v
Infrastructure
```

## Command

用于修改系统状态的业务操作。

## Query

用于查询数据，不改变领域状态。

## Handler

负责协调领域对象、仓储以及外部服务。

后续将增加：

- Pipeline Behavior
- Validation
- Transaction Boundary
- Dependency Injection
