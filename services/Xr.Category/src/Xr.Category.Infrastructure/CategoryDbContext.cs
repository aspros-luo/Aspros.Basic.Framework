using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using CategoryEntity = Xr.Category.Domain.Category;

namespace Xr.Category.Infrastructure;

/// <summary>
/// Database context owned by the Category service.
/// </summary>
public sealed class CategoryDbContext(
    DbContextOptions<CategoryDbContext> options)
    : DbContext(options), IDbContext
{
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    public DbSet<CategoryCapValidationReceipt> CapValidationReceipts =>
        Set<CategoryCapValidationReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddEntityConfigurationsFromAssembly(
            typeof(CategoryDbContext).Assembly);

        modelBuilder.Entity<CategoryCapValidationReceipt>(entity =>
        {
            entity.ToTable("category_cap_validation_receipt");
            entity.HasKey(x => x.EventId);

            entity.Property(x => x.EventId)
                .HasColumnName("event_id")
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(x => x.RunId)
                .HasColumnName("run_id")
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(x => x.Scenario)
                .HasColumnName("scenario")
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(x => x.ProcessedAt)
                .HasColumnName("processed_at")
                .HasPrecision(6)
                .IsRequired();

            entity.HasIndex(x => x.RunId)
                .HasDatabaseName("ix_category_cap_receipt_run");
        });
    }
}
