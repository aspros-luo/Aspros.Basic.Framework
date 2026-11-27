using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Aspros.Base.Framework.RAG.Hosting;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;
using Rag.KnowledgeBase.Api;

const string DemoTenantId = "demo-tenant";

var builder = WebApplication.CreateBuilder(args);
var databasePath = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Database:Path"] ?? "App_Data/rag-demo.db"));
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<DemoRagDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddSingleton<DemoDocumentStorage>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddRagEntityFrameworkCore<DemoRagDbContext>();
builder.Services.AddRagWorker<DemoIngestionJobHandler>(options =>
{
    options.MaxConcurrency = 2;
    options.LeaseDuration = TimeSpan.FromMinutes(2);
    options.RenewalInterval = TimeSpan.FromSeconds(30);
    options.PollingInterval = TimeSpan.FromSeconds(1);
});

var app = builder.Build();

// This is an isolated sample database. Production applications must manage their own
// schema lifecycle and must not copy this EnsureCreated behavior into business services.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemoRagDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="zh-CN">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aspros RAG Demo</title>
<style>
body{font:16px system-ui,sans-serif;max-width:850px;margin:48px auto;padding:0 20px;color:#202938}
h1{margin-bottom:8px} .muted{color:#64748b} code{background:#f1f5f9;padding:3px 6px;border-radius:4px}
li{margin:10px 0} a{color:#2563eb}
</style></head>
<body>
<h1>Aspros 企业知识库 RAG Demo</h1>
<p class="muted">第一阶段：知识库、文档上传、持久化任务、后台文本切片。</p>
<h2>快速体验</h2>
<ol>
<li>创建知识库：<code>POST /api/knowledge-bases</code></li>
<li>上传 TXT / Markdown：<code>POST /api/knowledge-bases/{id}/documents</code>，multipart 字段名为 <code>file</code></li>
<li>查看任务：<code>GET /api/ingestion-jobs/{jobId}</code></li>
<li>查看切片：<code>GET /api/documents/{documentId}/chunks</code></li>
</ol>
<p>API 描述：<a href="/openapi/v1.json">OpenAPI JSON</a></p>
<p class="muted">当前版本尚未启用 Embedding、向量索引和 LLM 问答；不会用模拟结果冒充真实 AI 能力。</p>
</body></html>
""", "text/html; charset=utf-8"));

app.MapGet("/health", () => Results.Ok(new
{
    service = "rag-knowledge-base-demo",
    status = "ok",
    phase = "document-ingestion-and-chunking",
    aiIndexingEnabled = false
}));

app.MapPost("/api/knowledge-bases", async (
    CreateKnowledgeBaseRequest request,
    DemoRagDbContext db,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Knowledge base name is required." });
    }

    var knowledgeBase = KnowledgeBase.Create(DemoTenantId, request.Name);
    db.Add(knowledgeBase);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/knowledge-bases/{knowledgeBase.Id}", new
    {
        knowledgeBase.Id,
        knowledgeBase.Name,
        knowledgeBase.TenantId,
        knowledgeBase.CreatedAtUtc
    });
});

app.MapGet("/api/knowledge-bases", async (
    DemoRagDbContext db,
    CancellationToken cancellationToken) =>
{
    var items = await db.Set<KnowledgeBase>()
        .AsNoTracking()
        .Where(x => x.TenantId == DemoTenantId && !x.IsArchived)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new { x.Id, x.Name, x.CreatedAtUtc })
        .ToListAsync(cancellationToken);
    return Results.Ok(items);
});

app.MapPost("/api/knowledge-bases/{knowledgeBaseId:guid}/documents", async (
    Guid knowledgeBaseId,
    HttpRequest request,
    DemoRagDbContext db,
    DemoDocumentStorage storage,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Use multipart/form-data with a file field named 'file'." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "A non-empty file field named 'file' is required." });
    }

    if (file.Length > 10 * 1024 * 1024)
    {
        return Results.BadRequest(new { error = "The maximum document size is 10 MB." });
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (extension is not ".txt" and not ".md" and not ".markdown")
    {
        return Results.BadRequest(new { error = "Only .txt, .md, and .markdown files are supported in this demo phase." });
    }

    var knowledgeBase = await db.Set<KnowledgeBase>()
        .SingleOrDefaultAsync(x => x.Id == knowledgeBaseId &&
                                  x.TenantId == DemoTenantId &&
                                  !x.IsArchived,
            cancellationToken);
    if (knowledgeBase is null)
    {
        return Results.NotFound(new { error = "Knowledge base not found." });
    }

    StoredDocument stored;
    await using (var input = file.OpenReadStream())
    {
        try
        {
            stored = await storage.SaveAsync(input, extension, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    try
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var title = Path.GetFileNameWithoutExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Untitled document";
        }

        var document = Document.Create(
            DemoTenantId,
            knowledgeBaseId,
            title.Length > 500 ? title[..500] : title,
            stored.Key,
            stored.ContentHash);

        var mediaType = extension == ".txt" ? "text/plain" : "text/markdown";
        var version = DocumentVersion.Create(
            document.Id,
            1,
            stored.ContentHash,
            mediaType,
            stored.Key,
            "paragraph-v1");
        document.SetCurrentVersion(version);

        var job = IngestionJob.Create(
            DemoTenantId,
            knowledgeBaseId,
            document.Id,
            version.Id,
            $"ingest:{document.Id:N}:v1");

        db.Add(document);
        db.Add(version);
        db.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Accepted($"/api/ingestion-jobs/{job.Id}", new
        {
            documentId = document.Id,
            document.Title,
            versionId = version.Id,
            version.VersionNumber,
            jobId = job.Id,
            job.Status,
            stored.ContentHash
        });
    }
    catch
    {
        await storage.DeleteAsync(stored.Key);
        throw;
    }
});

app.MapGet("/api/knowledge-bases/{knowledgeBaseId:guid}/documents", async (
    Guid knowledgeBaseId,
    DemoRagDbContext db,
    CancellationToken cancellationToken) =>
{
    var exists = await db.Set<KnowledgeBase>()
        .AnyAsync(x => x.Id == knowledgeBaseId && x.TenantId == DemoTenantId, cancellationToken);
    if (!exists)
    {
        return Results.NotFound();
    }

    var documents = await db.Set<Document>()
        .AsNoTracking()
        .Where(x => x.KnowledgeBaseId == knowledgeBaseId && x.TenantId == DemoTenantId)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new
        {
            x.Id,
            x.Title,
            x.CurrentVersionNumber,
            x.Status,
            x.CreatedAtUtc,
            ChunkCount = db.Set<DocumentChunk>().Count(chunk =>
                chunk.DocumentId == x.Id && chunk.DocumentVersionId == x.CurrentVersionId)
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(documents);
});

app.MapGet("/api/ingestion-jobs/{jobId:guid}", async (
    Guid jobId,
    DemoRagDbContext db,
    CancellationToken cancellationToken) =>
{
    var job = await db.Set<IngestionJob>()
        .AsNoTracking()
        .Where(x => x.Id == jobId && x.TenantId == DemoTenantId)
        .Select(x => new
        {
            x.Id,
            x.DocumentId,
            x.DocumentVersionId,
            Status = x.Status.ToString(),
            x.AttemptCount,
            x.MaxAttempts,
            x.LastError,
            x.CreatedAtUtc,
            x.UpdatedAtUtc,
            x.CompletedAtUtc
        })
        .SingleOrDefaultAsync(cancellationToken);

    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.MapGet("/api/documents/{documentId:guid}/chunks", async (
    Guid documentId,
    DemoRagDbContext db,
    CancellationToken cancellationToken) =>
{
    var documentExists = await db.Set<Document>()
        .AnyAsync(x => x.Id == documentId && x.TenantId == DemoTenantId, cancellationToken);
    if (!documentExists)
    {
        return Results.NotFound();
    }

    var chunks = await db.Set<DocumentChunk>()
        .AsNoTracking()
        .Where(x => x.DocumentId == documentId && x.TenantId == DemoTenantId)
        .OrderBy(x => x.ChunkIndex)
        .Select(x => new
        {
            x.Id,
            x.ChunkIndex,
            x.Content,
            x.ContentHash,
            x.SourceLocation,
            x.CreatedAtUtc
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(chunks);
});

app.Run();

public sealed record CreateKnowledgeBaseRequest(string Name);

public partial class Program { }
