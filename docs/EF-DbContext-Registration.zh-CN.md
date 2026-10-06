# EF Core DbContext 注册

业务服务通常需要重复写两次注册：

```csharp
builder.Services.AddDbContext<TradeDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddScoped<IDbContext, TradeDbContext>();
```

Framework 提供一个小型便捷封装：

```csharp
builder.Services.AddAsprosDbContext<TradeDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));
```

数据库 Provider、连接字符串、Migration Assembly 以及其他 EF 配置仍然清晰地写在业务服务中，不会被 Framework 隐藏。

`IDbContext` 会解析到同一个 Scoped `TradeDbContext` 实例，因此不会破坏现有 Repository 和 UnitOfWork 的设计。

该方法保持按需使用；如果业务服务有特殊注册策略，仍然可以直接使用标准 `AddDbContext`。