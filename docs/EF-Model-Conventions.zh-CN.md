# EF Core 模型约定

## 目的

真实业务服务中经常重复一类 EF Core 基础代码：扫描业务程序集中的实体 Mapping、在 `OnModelCreating` 中自动执行 Mapping，以及在不覆盖已有条件的情况下组合全局 QueryFilter。

现在由 Framework 负责这套通用机制。业务服务仍然拥有自己的 DbContext、实体、Mapping、租户模型以及业务过滤条件。

## 自动实体映射

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.AddEntityConfigurationsFromAssembly(GetType().Assembly);
}
```

业务 Mapping 仍然放在业务服务中：

```csharp
public sealed class TradeMap : EntityMappingConfiguration<Trade>
{
    public override void Map(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("trade");
        builder.HasKey(x => x.Id);
    }
}
```

`EntityMappingConfiguration<T>` 保持足够小，不强制表名、索引、字段约定或业务规则。

## 全局 QueryFilter

租户、软删除等跨实体过滤可以使用：

```csharp
modelBuilder.AddQueryFilter<TenantEntity>(
    entity => entity.TenantId == currentTenantId);
```

如果实体已经存在 QueryFilter，Framework 会把旧条件与新条件按 `AND` 组合，而不是静默覆盖旧条件。

Framework 不定义具体 TenantEntity 或租户值来源，这些仍属于业务服务。

## 为什么应该放进 Framework

这不是为了“架构完整”凭空增加的能力，而是从真实业务服务中反向提炼出来的基础设施。Category/Commodity 类服务已经存在相同的程序集扫描 Mapping 模式，而 `IDbContext`、`BaseRepository<T>` 也已经形成共同的持久化契约。

边界保持明确：

- Framework：负责扫描、执行 Mapping、组合表达式。
- 业务服务：负责 DbContext、实体、Mapping、租户上下文和业务过滤规则。
- Repository：负责业务特有查询。

这样新建 `Trade` 类微服务时可以减少重复基础代码，同时不会把 Framework 变成业务模型框架。