using Aspros.Base.Framework.Infrastructure;
using Flurl.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Aspros.SaaS.System.Infrastructure;

public static class PermissionExtensions
{
    public static IApplicationBuilder UsePermissionValid(
        this IApplicationBuilder builder)
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
        IServiceDiscovery serviceDiscovery,
        IWorkContext workContext,
        IOptions<PermissionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceDiscovery);
        ArgumentNullException.ThrowIfNull(workContext);
        ArgumentNullException.ThrowIfNull(options);

        var endpoint = GetEndpoint(context);
        var permission = endpoint?.Metadata.GetMetadata<Permission>();

        if (permission is null)
        {
            await _next(context);
            return;
        }

        var userId = await workContext.GetUserId();
        var settings = options.Value;

        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ServiceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.GroupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ValidationPath);

        ServiceEndpoint? instance;

        try
        {
            instance = await serviceDiscovery.GetHealthyEndpointAsync(
                settings.ServiceName,
                settings.GroupName,
                context.RequestAborted);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "权限服务发现失败");
            return;
        }

        if (instance is null)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "权限服务不可用");
            return;
        }

        var url = new UriBuilder(instance.Address)
        {
            Path = settings.ValidationPath,
            Query =
                $"PermissionCode={Uri.EscapeDataString(permission.Code)}&userId={userId}"
        }.Uri.ToString();

        try
        {
            var policy = PollyExtend.GetRetryPolicy();

            var response = await policy.ExecuteAsync(
                async () =>
                {
                    var flurlResponse = await url
                        .AllowAnyHttpStatus()
                        .GetAsync(cancellationToken: context.RequestAborted);

                    return flurlResponse.ResponseMessage;
                });

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
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
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
