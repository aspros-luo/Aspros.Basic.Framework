using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aspros.Base.Framework.RAG.EntityFrameworkCore;

internal sealed class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
{
    public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
    {
        builder.ToTable("rag_knowledge_bases");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.TenantId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.UpdatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.HasIndex(x => new { x.TenantId, x.IsArchived });
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("rag_documents");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.KnowledgeBaseId, x.Id });
        builder.Property(x => x.TenantId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SourceUri).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.UpdatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.DeletedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.HasOne<KnowledgeBase>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.KnowledgeBaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ActiveIndexGenerationId);
        builder.HasIndex(x => new { x.TenantId, x.KnowledgeBaseId, x.Status });
    }
}

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("rag_document_versions");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.DocumentId, x.Id });
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.MediaType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SourceUri).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ChunkingProfileVersion).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
    }
}

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("rag_document_chunks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SectionTitle).HasMaxLength(500);
        builder.Property(x => x.SourceLocation).HasMaxLength(2048);
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.HasOne<KnowledgeBase>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.KnowledgeBaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.KnowledgeBaseId, x.DocumentId })
            .HasPrincipalKey(x => new { x.TenantId, x.KnowledgeBaseId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(x => new { x.DocumentId, x.DocumentVersionId })
            .HasPrincipalKey(x => new { x.DocumentId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DocumentVersionId, x.ChunkIndex }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.KnowledgeBaseId, x.DocumentId });
    }
}

internal sealed class IndexGenerationConfiguration : IEntityTypeConfiguration<IndexGeneration>
{
    public void Configure(EntityTypeBuilder<IndexGeneration> builder)
    {
        builder.ToTable("rag_index_generations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EmbeddingModelKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ChunkingProfileVersion).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.ActivatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.RetiredAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.HasOne<KnowledgeBase>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.KnowledgeBaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.KnowledgeBaseId, x.DocumentId })
            .HasPrincipalKey(x => new { x.TenantId, x.KnowledgeBaseId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(x => new { x.DocumentId, x.DocumentVersionId })
            .HasPrincipalKey(x => new { x.DocumentId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DocumentId, x.DocumentVersionId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.KnowledgeBaseId, x.Status });
    }
}

internal sealed class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> builder)
    {
        builder.ToTable("rag_ingestion_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.JobType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.LeaseOwner).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.NextAttemptAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.CreatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.UpdatedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.StartedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.CompletedAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.Property(x => x.LeaseExpiresAtUtc).HasConversion(new UtcDateTimeOffsetToTicksConverter());
        builder.HasOne<KnowledgeBase>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.KnowledgeBaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.KnowledgeBaseId, x.DocumentId })
            .HasPrincipalKey(x => new { x.TenantId, x.KnowledgeBaseId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>()
            .WithMany()
            .HasForeignKey(x => new { x.DocumentId, x.DocumentVersionId })
            .HasPrincipalKey(x => new { x.DocumentId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.DocumentId, x.DocumentVersionId, x.JobType, x.Status });
    }
}
