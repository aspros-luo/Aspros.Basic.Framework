namespace Aspros.Base.Framework.Infrastructure;

public sealed class PermissionOptions
{
    public string ServiceName { get; set; } = "saas-system";

    public string GroupName { get; set; } = "DEFAULT_GROUP";

    public string ValidationPath { get; set; } =
        "/system/user.permission.valid";
}
