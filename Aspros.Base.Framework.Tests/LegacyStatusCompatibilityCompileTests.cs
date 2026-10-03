using Aspros.Base.Framework.Domain.ValueObjects;

namespace Aspros.Base.Framework.Tests;

public abstract class LegacyStatusSyntaxOwner
{
    public static class Status
    {
        public static EntityStatus Normal => EntityStatus.Normal;
        public static EntityStatus Deleted => EntityStatus.Deleted;
        public static EntityStatus Invalid => EntityStatus.Invalid;
    }
}

public class LegacyStatusSyntaxEntity : LegacyStatusSyntaxOwner
{
    public EntityStatus Status { get; set; } = EntityStatus.Normal;
}

public sealed class LegacyStatusSyntaxConsumer : LegacyStatusSyntaxEntity
{
    public void UnFollow()
    {
        Status = Status.Deleted;
    }

    public void Follow()
    {
        Status = Status.Normal;
    }
}
