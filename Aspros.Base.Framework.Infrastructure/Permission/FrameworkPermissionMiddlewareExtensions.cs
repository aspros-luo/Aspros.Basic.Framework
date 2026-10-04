using Microsoft.AspNetCore.Builder;

namespace Aspros.Base.Framework.Infrastructure;

public static class FrameworkPermissionMiddlewareExtensions
{
    public static IApplicationBuilder UseFrameworkPermissionValidation(
        this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseMiddleware<FrameworkPermissionMiddleware>();
    }
}
