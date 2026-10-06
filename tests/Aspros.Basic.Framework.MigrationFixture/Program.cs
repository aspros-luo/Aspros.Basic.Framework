using Aspros.Basic.Framework.MigrationFixture;
using Microsoft.EntityFrameworkCore;

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__MigrationDatabase")
    ?? throw new InvalidOperationException(
        "ConnectionStrings__MigrationDatabase is required.");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MigrationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

if (args.Contains("--reset", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
    await db.Database.OpenConnectionAsync();

    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.CommandText = """
        DROP TABLE IF EXISTS MigrationCustomers;
        DROP TABLE IF EXISTS __FrameworkMigrationFixtureHistory;
        """;
    await command.ExecuteNonQueryAsync();
    return;
}

if (args.Contains("--seed-v1", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

    await db.Database.MigrateAsync();

    if (!await db.Customers.AnyAsync())
    {
        var customer = new MigrationCustomer();
        typeof(MigrationCustomer)
            .GetProperty("Name")!
            .SetValue(customer, "preserved-before-rename");

        db.Customers.Add(customer);
        await db.SaveChangesAsync();
    }

    return;
}

if (args.Contains("--verify-v2", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText =
        "SELECT DisplayName FROM MigrationCustomers WHERE Id = 1";

    var value = await command.ExecuteScalarAsync();

    if (!string.Equals(value?.ToString(), "preserved-before-rename", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Migration verification failed. Expected preserved-before-rename, got '{value}'.");
    }

    Console.WriteLine("Migration rename verification passed.");
    return;
}

app.MapGet("/health", () => Results.Ok("ok"));
app.Run();

public partial class Program { }
