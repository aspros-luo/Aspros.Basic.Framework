using Aspros.Base.Framework.Domain;

namespace Xr.Category.Domain;

/// <summary>
/// A small relational aggregate used to validate the Framework's EF Core CRUD path.
/// This is an integration fixture, not the finalized Category business model.
/// </summary>
public sealed class Category : BaseEntity, IAggregateRoot
{
    private Category()
    {
    }

    public Category(
        string code,
        string name,
        long? parentId,
        int sortOrder,
        bool isEnabled)
    {
        Update(code, name, parentId, sortOrder, isEnabled);
    }

    public long Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public long? ParentId { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public Category? Parent { get; private set; }

    public ICollection<Category> Children { get; private set; } =
        new List<Category>();

    public void Update(
        string code,
        string name,
        long? parentId,
        int sortOrder,
        bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Id != 0 && parentId == Id)
        {
            throw new ArgumentException(
                "A category cannot be its own parent.",
                nameof(parentId));
        }

        Code = code.Trim();
        Name = name.Trim();
        ParentId = parentId;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
    }
}
