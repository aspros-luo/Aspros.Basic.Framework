using Aspros.Base.Framework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class FrameworkUnitOfWorkRegressionTests
{
    [Fact]
    public async Task UnitOfWork_CommitWithoutChangesIsSuccessful()
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

        Assert.True(await uow.CommitAsync());
        Assert.True(await uow.CommitAsync(CancellationToken.None));
    }

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
    public async Task UnitOfWork_ExplicitTransactionCommitsChanges()
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
        await uow.RegisterNew(new SqliteRegressionRow { Name = "transaction-commit" });

        Assert.True(await uow.CommitAsync());

        await using var observer = new SqliteRegressionDbContext(options);
        Assert.Equal(1, await observer.Rows.CountAsync(x => x.Name == "transaction-commit"));
    }

    [Fact]
    public async Task UnitOfWork_DoesNotCommitExternallyOwnedTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteRegressionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var writer = new SqliteRegressionDbContext(options);
        await writer.Database.EnsureCreatedAsync();
        await using var externalTransaction = await writer.Database.BeginTransactionAsync();

        var services = new ServiceCollection();
        services.AddScoped<IWorkContext, TestWorkContext>();
        services.AddScoped<IDbContext>(_ => writer);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        uow.BeginTransaction(externalTransaction);
        await uow.RegisterNew(new SqliteRegressionRow { Name = "external-transaction" });

        Assert.True(await uow.CommitAsync());
        Assert.Same(externalTransaction, uow.DbContextTransaction);

        // The outer owner still controls the transaction and can roll it back.
        await externalTransaction.RollbackAsync();

        await using var observer = new SqliteRegressionDbContext(options);
        Assert.Equal(0, await observer.Rows.CountAsync());
    }

    [Fact]
    public async Task UnitOfWork_DoesNotRollbackExternallyOwnedTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteRegressionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var writer = new SqliteRegressionDbContext(options);
        await writer.Database.EnsureCreatedAsync();
        await using var externalTransaction = await writer.Database.BeginTransactionAsync();

        var services = new ServiceCollection();
        services.AddScoped<IWorkContext, TestWorkContext>();
        services.AddScoped<IDbContext>(_ => writer);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        uow.BeginTransaction(externalTransaction);
        await uow.RegisterNew(new SqliteRegressionRow { Name = "external-rollback" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => uow.RollbackAsync());

        Assert.Same(externalTransaction, uow.DbContextTransaction);
        await externalTransaction.RollbackAsync();
        writer.ClearTrackedChanges();

        await using var observer = new SqliteRegressionDbContext(options);
        Assert.Equal(0, await observer.Rows.CountAsync());
    }

    [Fact]
    public async Task UnitOfWork_RegisterDirty_PreservesTrackedPropertyChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SqliteRegressionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var writer = new SqliteRegressionDbContext(options);
        await writer.Database.EnsureCreatedAsync();
        writer.Rows.Add(new SqliteRegressionRow { Name = "before" });
        await writer.SaveChangesAsync();

        var row = await writer.Rows.SingleAsync();
        row.Name = "after";

        var services = new ServiceCollection();
        services.AddScoped<IWorkContext, TestWorkContext>();
        services.AddScoped<IDbContext>(_ => writer);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await uow.RegisterDirty(row);
        writer.ChangeTracker.DetectChanges();

        var modifiedProperties = writer.Entry(row).Properties
            .Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .ToArray();

        Assert.Equal(new[] { "Name" }, modifiedProperties);
        Assert.True(await uow.CommitAsync());

        await using var observer = new SqliteRegressionDbContext(options);
        Assert.Equal("after", await observer.Rows.Select(x => x.Name).SingleAsync());
    }

    [Fact]
    public async Task DapperExecutor_UsesUnitOfWorkTransaction()
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
        services.AddScoped<IDapperExecutor, DapperExecutor>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dapper = scope.ServiceProvider.GetRequiredService<IDapperExecutor>();

        await uow.BeginTransactionAsync();

        await dapper.ExecuteAsync(
            "INSERT INTO FrameworkRegressionRows (Name) VALUES (@Name)",
            new { Name = "dapper-rolled-back" });

        await uow.RollbackAsync();

        await using var observer = new SqliteRegressionDbContext(options);
        Assert.Equal(0, await observer.Rows.CountAsync(x => x.Name == "dapper-rolled-back"));
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
