# EF Core Model Conventions

## Purpose

Real business services often repeat the same EF Core infrastructure: discover mapping classes in the business assembly, invoke them during `OnModelCreating`, and combine cross-cutting query filters without overwriting existing filters.

The Framework now owns this mechanism. Business services still own their DbContext, entities, mapping rules, tenant model and business-specific filters.

## Automatic entity mapping

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.AddEntityConfigurationsFromAssembly(GetType().Assembly);
}
```

Business mapping remains local:

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

`EntityMappingConfiguration<T>` is intentionally small. It does not impose table naming, indexes, conventions or business rules.

## Global query filters

Cross-cutting filters can be composed with:

```csharp
modelBuilder.AddQueryFilter<TenantEntity>(
    entity => entity.TenantId == currentTenantId);
```

If an entity already has a query filter, the Framework combines the existing filter and the new filter with `AND` semantics. It does not silently replace the existing rule.

The Framework does not define a concrete tenant model or tenant provider. Those remain business-service concerns.

## Why this belongs in Framework

This capability was extracted from real service code rather than added as an architectural abstraction in isolation. The same assembly-scanning pattern existed in the Category/Commodity-style infrastructure, while `IDbContext` and `BaseRepository<T>` already formed the common persistence contract.

The extraction boundary is deliberate:

- Framework: scanning, configuration invocation and expression composition.
- Business service: DbContext, entity, mapping class, tenant context and business filters.
- Repository: business-specific queries.

This keeps a new `Trade`-like service small without turning Framework into a business model framework.