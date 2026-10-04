using Grpc.AspNetCore.Server;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace Aspros.Base.Framework.Infrastructure;

public static class FrameworkGrpcExtensions
{
    public static IServiceCollection AddFrameworkGrpc(
        this IServiceCollection services,
        Action<GrpcServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddGrpc(configure);
        return services;
    }

    public static IHttpClientBuilder AddFrameworkGrpcClient<TClient>(
        this IServiceCollection services,
        Uri address)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(address);

        return services.AddGrpcClient<TClient>(
            options => options.Address = address);
    }

    public static IHttpClientBuilder AddFrameworkGrpcClient<TClient>(
        this IServiceCollection services,
        string name,
        Uri address)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);

        return services.AddGrpcClient<TClient>(
            name,
            options => options.Address = address);
    }

    /// <summary>
    /// Opt-in propagation of the current HTTP Bearer token to an outgoing gRPC call.
    /// Useful for API -> service -> service user-context propagation.
    /// </summary>
    public static IHttpClientBuilder ForwardAuthorizationHeader(
        this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHttpContextAccessor();

        return builder.AddCallCredentials(
            (context, metadata, serviceProvider) =>
            {
                var httpContextAccessor =
                    serviceProvider.GetService<IHttpContextAccessor>();

                var httpContext = httpContextAccessor?.HttpContext;

                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                {
                    return Task.CompletedTask;
                }

                var authorization =
                    httpContext.Request.Headers.Authorization.ToString();

                if (!AuthenticationHeaderValue.TryParse(
                        authorization,
                        out var header) ||
                    !string.Equals(
                        header.Scheme,
                        "Bearer",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(header.Parameter))
                {
                    return Task.CompletedTask;
                }

                metadata.Add(
                    "authorization",
                    $"Bearer {header.Parameter}");

                return Task.CompletedTask;
            });
    }

    public static IHttpClientBuilder PropagateGrpcCallContext(
        this IHttpClientBuilder builder,
        bool suppressMissingContextErrors = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return suppressMissingContextErrors
            ? builder.EnableCallContextPropagation(options =>
                options.SuppressContextNotFoundErrors = true)
            : builder.EnableCallContextPropagation();
    }

    public static IEndpointConventionBuilder MapFrameworkGrpcService<TService>(
        this IEndpointRouteBuilder endpoints)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGrpcService<TService>();
    }
}
