using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xr.Category.Domain;

namespace Xr.Category.Infrastructure;

public sealed class CategoryMapping : EntityMappingConfiguration<Category>
{
    public override void Map(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("category");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ParentId)
            .HasColumnName("parent_id");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(x => x.Creator)
            .HasColumnName("creator_id")
            .IsRequired();

        builder.Property(x => x.CreateTime)
            .HasColumnName("create_time")
            .HasPrecision(6)
            .IsRequired();

        builder.Property(x => x.Updater)
            .HasColumnName("updater_id")
            .IsRequired();

        builder.Property(x => x.UpdateTime)
            .HasColumnName("update_time")
            .HasPrecision(6)
            .IsRequired();

        builder.Property(x => x.Deleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_category_code");

        builder.HasIndex(x => new { x.ParentId, x.SortOrder })
            .HasDatabaseName("ix_category_parent_sort");

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_category_parent");
    }
}
