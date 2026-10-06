# 微服务可靠性能力

本文件定义 Framework 的可靠性边界：能力统一入口，策略按需启用。

- 分布式事务：优先本地事务 + CAP 可靠消息 + 最终一致性，不做跨服务 2PC。
- 重试：只用于可安全重复执行或具备幂等保护的操作。
- 熔断：阻止故障依赖形成级联故障。
- 超时：限制单次调用资源占用。
- 限流：服务保护与 Gateway/WAF 形成纵深防御。
- 降级：必须由业务定义结果，不能把失败写操作伪装成成功。
- Health：区分 Liveness 与 Readiness。
- 幂等：支付、订单、库存、账务等 Command 必须重点考虑。
- MQ：可靠发布、重试、Consumer Group 和最终一致性由 CAP/MQ 负责。
- 分布式锁：仅作为特定任务的基础设施能力，不替代幂等和数据库约束。

Framework 不强制 Redis、CAP、单一 MQ、Saga、2PC、CQRS 或 DDD。目标是让简单服务保持轻量，同时为复杂微服务提供标准可靠性入口。

示例：

    builder.Services.AddFrameworkCap<OrderDbContext>(options =>
    {
        options.UseRabbitMQ(configuration["RabbitMQ:ConnectionString"]!);
    });

    builder.Services.AddFrameworkResilientHttpClient("permission");
    builder.Services.AddFrameworkHealthChecks();
    app.MapFrameworkHealthChecks();
