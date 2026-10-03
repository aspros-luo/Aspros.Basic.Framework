using Aspros.Base.Framework.Application.Abstractions.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aspros.Base.Framework.Infrastructure
{
    public static class PermissionExtensions
    {
        public static IApplicationBuilder UsePermissionValid(this IApplicationBuilder builder)
            => builder.UseMiddleware<PermissionMiddleware>();
    }

    /// <summary>
    /// Generic permission middleware。
    /// Framework 只负责识别 Permission 元数据并调用业务侧 IPermissionChecker，不绑定 Nacos、Flurl、服务名或某个具体权限服务。
    /// </summary>
    public sealed class PermissionMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var permission = context.GetEndpoint()?.Metadata.GetMetadata<Permission>();
            if (permission is not null)
            {
                var checker = context.RequestServices.GetService<IPermissionChecker>();
                var workContext = context.RequestServices.GetService<IWorkContext>();

                if (checker is null || workContext is null)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsync("Permission validation is not configured.");
                    return;
                }

                var userId = await workContext.GetUserId();
                if (userId == 0 || !await checker.HasPermissionAsync(userId, permission.Code, context.RequestAborted))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            await _next(context);
        }
    }
}
