using Flurl.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Nacos.V2;
using Polly;
using System.Net.Http.Json;

namespace Aspros.SaaS.System.Infrastructure;

public static class PermissionExtensions
{
    public static IApplicationBuilder UsePermissionValid(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseMiddleware<PermissionMiddleware>();
    }
}

public sealed class PermissionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next =
        next ?? throw new ArgumentNullException(nameof(next));

    public static Endpoint? GetEndpoint(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Features.Get<IEndpointFeature>()?.Endpoint;
    }

    public async Task Invoke(
        HttpContext context,
        INacosNamingService namingService,
        IWorkContext workContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(namingService);
        ArgumentNullException.ThrowIfNull(workContext);

        var endpoint = GetEndpoint(context);
        var permission = endpoint?.Metadata.GetMetadata<Permission>();

        if (permission is null)
        {
            await _next(context);
            return;
        }

        var userId = await workContext.GetUserId();
        var instance = await namingService.SelectOneHealthyInstance(
            "saas-system",
            "DEFAULT_GROUP");

        if (instance is null)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "权限服务不可用");
            return;
        }

        var host = $"{instance.Ip}:{instance.Port}";
        var baseUrl = instance.Metadata.TryGetValue("secure", out _)
            ? $"https://{host}"
            : $"http://{host}";

        var url =
            $"{baseUrl}/system/user.permission.valid" +
            $"?PermissionCode={Uri.EscapeDataString(permission.Code)}" +
            $"&userId={userId}";

        try
        {
            var policy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(response => !response.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var response = await policy.ExecuteAsync(
                () => url
                    .AllowAnyHttpStatus()
                    .GetAsync(cancellationToken: context.RequestAborted));

            if (!response.IsSuccessStatusCode)
            {
                await WriteErrorAsync(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "权限服务请求失败");
                return;
            }

            var allowed = await response.Content.ReadFromJsonAsync<bool>(
                context.RequestAborted);

            if (allowed != true)
            {
                await WriteErrorAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "当前用户权限不够");
                return;
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "权限服务不可用");
            return;
        }

        await _next(context);
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            new { is_success = false, msg = message },
            context.RequestAborted);
    }
}
