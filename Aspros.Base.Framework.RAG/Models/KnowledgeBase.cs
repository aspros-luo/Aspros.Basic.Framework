namespace Aspros.Base.Framework.RAG.Models;

/// <summary>
/// A tenant-owned knowledge base. Tenant identity is supplied by the host application;
/// this package does not define users, roles, or tenant authorization.
/// </summary>
public sealed class KnowledgeBase
{
    private KnowledgeBase() { }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static KnowledgeBase Create(
        string tenantId,
        string name,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new KnowledgeBase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Trim(),
            Name = name.Trim(),
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
    }

    public void Archive(DateTimeOffset? now = null)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }
}
