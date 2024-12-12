using Aspros.Base.Framework.Infrastructure;
using Flurl.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Nacos.V2;


namespace Aspros.SaaS.System.Infrastructure
{
    public static class PermissionExtensions
    {
        public static IApplicationBuilder UsePermissionValid(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PermissionMiddleware>();
        }
    }

    public class PermissionMiddleware(RequestDelegate next)
    {
        public static Endpoint GetEndpoint(HttpContext context)
        {
            return context == null ? throw new ArgumentNullException(nameof(context)) : (context.Features.Get<IEndpointFeature>()?.Endpoint);
        }
        private readonly RequestDelegate _next = next;
        public async Task Invoke(HttpContext context, INacosNamingService _namingService, IWorkContext _workContext)
        {
            var endpoint = GetEndpoint(context);
            if (endpoint != null)
            {
                var permission = endpoint.Metadata.GetMetadata<Permission>();
                if (permission != null)
                {
                    var userId = await _workContext.GetUserId();

                    var code = permission.Code;

                    var instance = await _namingService.SelectOneHealthyInstance("saas-system", "DEFAULT_GROUP");

                    var host = $"{instance.Ip}:{instance.Port}";

                    var baseUrl = instance.Metadata.TryGetValue("secure", out _) ? $"https://{host}" : $"http://{host}";

                    var url = $"{baseUrl}/system/user.permission.valid?PermissionCode={code}&userId={userId}";
                    await Console.Out.WriteLineAsync($"权限接口地址:{url}");
                    var result = await url.GetJsonAsync<bool>();

                    if (!result) throw new Exception("当前用户权限不够");

                }
            }
            await _next.Invoke(context);
        }
    }
}
