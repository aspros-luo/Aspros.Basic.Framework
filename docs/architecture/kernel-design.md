# Framework v10 Kernel 设计说明

## 1. 目标

Kernel 是 Framework v10 的领域基础层，提供 DDD 开发所需的通用抽象。

设计原则：

- 不依赖 ORM
- 不依赖消息中间件
- 不依赖 Web 框架
- 保持 Domain 层纯净

## 2. 核心组件

### Entity

实体通过唯一标识区分对象身份。

适用于：

- 用户
- 订单
- 商品

### AggregateRoot

聚合根负责维护业务一致性边界，并保存领域事件。

### Domain Event

领域事件用于描述已经发生的业务事实。

例如：

- UserCreated
- OrderPaid
- PaymentCompleted

后续可以连接：

Domain Event -> Application Handler -> MQ

## 3. 演进方向

后续版本将增加：

- ValueObject
- Result Pattern
- Specification
- Domain Service
