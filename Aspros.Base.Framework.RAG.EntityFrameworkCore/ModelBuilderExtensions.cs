using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.RAG.EntityFrameworkCore;

/// <summary>
/// Adds the RAG persistence model to an application-owned DbContext.
/// The host remains responsible for its DbContext, provider, connection and migrations.
/// </summary>
public static class ModelBuilderExtensions
{
    public static ModelBuilder ApplyRagModel(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new KnowledgeBaseConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentChunkConfiguration());
        modelBuilder.ApplyConfiguration(new IndexGenerationConfiguration());
        modelBuilder.ApplyConfiguration(new IngestionJobConfiguration());

        return modelBuilder;
    }
}
