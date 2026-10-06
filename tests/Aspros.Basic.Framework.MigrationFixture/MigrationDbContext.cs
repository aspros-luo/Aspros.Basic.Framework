using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aspros.Basic.Framework.MigrationFixture;

public sealed class MigrationDbContext(DbContextOptions<MigrationDbContext> options)
    : DbContext(options)
{
    public DbSet<MigrationCustomer> Customers => Set<MigrationCustomer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationCustomer>(entity =>
        {
            entity.ToTable("MigrationCustomers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
    }
}

public sealed class MigrationCustomer
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class MigrationDbContextFactory : IDesignTimeDbContextFactory<MigrationDbContext>
{
    public MigrationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__MigrationDatabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__MigrationDatabase is required.");

        var options = new DbContextOptionsBuilder<MigrationDbContext>()
            .UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsHistoryTable("__FrameworkMigrationFixtureHistory"))
            .Options;

        return new MigrationDbContext(options);
    }
}
