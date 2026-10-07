using Aspros.Base.Framework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class FrameworkUnitOfWorkRegressionTests
{
    [Fact]
    public async Task UnitOfWork_StagesChangesUntilCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteRegressionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var writer = new SqliteRegressionDbContext(options);
        await writer.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<IWorkContext, TestWorkContext>();
        services.AddScoped<IDbContext>(_ => writer);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.RegisterNew(new SqliteRegressionRow { Name = "staged" });

        await using (var observer = new SqliteRegressionDbContext(options))
        {
            Assert.Equal(0, await observer.Rows.CountAsync());
        }

        Assert.True(await uow.CommitAsync());

        await using (var observer = new SqliteRegressionDbContext(options))
        {
            Assert.Equal(1, await observer.Rows.CountAsync());
        }
    }

    [Fact]
    public async Task UnitOfWork_RollbackClearsTrackedChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteRegressionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var writer = new SqliteRegressionDbContext(options);
        await writer.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<IWorkContext, TestWorkContext>();
        services.AddScoped<IDbContext>(_ => writer);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var row = new SqliteRegressionRow { Name = "rolled-back" };

        await uow.RegisterNew(row);
        await uow.RollbackAsync();

        Assert.Empty(writer.ChangeTracker.Entries());

        await writer.SaveChangesAsync();

        await using var observer = new SqliteRegressionDbContext(options);
        Assert.Equal(0, await observer.Rows.CountAsync());
    }

    [Fact]
    public async Task UnitOfWork_ExplicitTransactionRollsBackDatabaseChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteRegressionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var writer = new SqliteRegressionDbContext(options);
        await writer.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        services.AddScoped<IWorkContext, TestWorkContext>();
        services.AddScoped<IDbContext>(_ => writer);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await uow.BeginTransactionAsync();
        await uow.RegisterNew(new SqliteRegressionRow { Name = "transaction-rollback" });
        await uow.RollbackAsync();

        await using var observer = new SqliteRegressionDbContext(options);
        Assert.Equal(0, await observer.Rows.CountAsync());
    }
}

internal sealed class SqliteRegressionDbContext(
    DbContextOptions<SqliteRegressionDbContext> options)
    : DbContext(options), IDbContext
{
    public DbSet<SqliteRegressionRow> Rows => Set<SqliteRegressionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqliteRegressionRow>(entity =>
        {
            entity.ToTable("FrameworkRegressionRows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
        });
    }
}

internal sealed class SqliteRegressionRow
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
