using Aspros.Base.Framework.Infrastructure;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Validates endpoint permissions through the discovered permission service.
/// 通过服务发现找到权限服务，并对当前 Endpoint 执行权限校验。
///
/// <para>
/// The middleware is opt-in through the framework permission extension; endpoints
/// without the Permission metadata continue through the normal pipeline.
/// 该中间件通过 Framework 权限扩展按需启用；没有 Permission 元数据的 Endpoint
/// 会直接进入正常请求管道，不会额外调用权限服务。
/// </para>
/// </summary>
/// <param name="next">The next middleware in the ASP.NET Core pipeline. / ASP.NET Core 管道中的下一个中间件。</param>
public class FrameworkPermissionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next =
        next ?? throw new ArgumentNullException(nameof(next));

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

        var endpoint =
            context.Features.Get<IEndpointFeature>()?.Endpoint;

        var permission =
            endpoint?.Metadata.GetMetadata<Permission>();

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
        catch (FlurlHttpException)
        {
            // Flurl may wrap transport failures in FlurlHttpException.
            // Flurl 也可能把底层传输异常包装成 FlurlHttpException，不能让依赖故障变成 500。
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
public class FrameworkPermissionMiddleware(RequestDelegate next)