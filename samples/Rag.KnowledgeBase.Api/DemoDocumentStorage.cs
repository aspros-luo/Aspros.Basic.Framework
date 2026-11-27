using System.Security.Cryptography;

namespace Rag.KnowledgeBase.Api;

public sealed class DemoDocumentStorage
{
    private const long MaxDocumentBytes = 10 * 1024 * 1024;
    private readonly string _rootPath;

    public DemoDocumentStorage(IHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:RootPath"] ?? "App_Data/documents";
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredDocument> SaveAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (extension is not ".txt" and not ".md" and not ".markdown")
        {
            throw new ArgumentException("Only .txt, .md, and .markdown files are supported.", nameof(extension));
        }

        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            throw new ArgumentException("The uploaded document is empty.", nameof(source));
        }

        if (buffer.Length > MaxDocumentBytes)
        {
            throw new ArgumentException("The maximum document size is 10 MB.", nameof(source));
        }

        var key = $"{Guid.NewGuid():N}{extension}";
        var path = ResolvePath(key);
        await File.WriteAllBytesAsync(path, buffer.ToArray(), cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))));
        return new StoredDocument(key, hash);
    }

    public Task<string> ReadTextAsync(string key, CancellationToken cancellationToken)
    {
        return File.ReadAllTextAsync(ResolvePath(key), cancellationToken);
    }

    public Task DeleteAsync(string key)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.GetFileName(key) != key)
        {
            throw new InvalidOperationException("Invalid document storage key.");
        }

        var path = Path.GetFullPath(Path.Combine(_rootPath, key));
        if (!path.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The document storage key is outside the configured storage root.");
        }

        return path;
    }
}

public sealed record StoredDocument(string Key, string ContentHash);
