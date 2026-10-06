# EF Core DbContext Registration

Business services normally need both registrations:

```csharp
builder.Services.AddDbContext<TradeDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddScoped<IDbContext, TradeDbContext>();
```

Framework provides a small convenience wrapper:

```csharp
builder.Services.AddAsprosDbContext<TradeDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));
```

The provider, connection string, migrations assembly and other EF options remain visible in the business service.

`IDbContext` resolves to the same scoped `TradeDbContext` instance. This preserves the existing Repository and UnitOfWork design without introducing another DbContext abstraction.

The helper is intentionally opt-in; services can continue using normal `AddDbContext` when they need a different registration strategy.