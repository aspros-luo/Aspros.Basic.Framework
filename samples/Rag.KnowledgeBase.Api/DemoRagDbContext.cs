using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Rag.KnowledgeBase.Api;

public sealed class DemoRagDbContext(DbContextOptions<DemoRagDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyRagModel();
    }
}
