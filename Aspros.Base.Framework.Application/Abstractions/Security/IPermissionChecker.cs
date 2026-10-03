namespace Aspros.Base.Framework.Application.Abstractions.Security;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(
        long userId,
        string permissionCode,
        CancellationToken cancellationToken = default);
}
