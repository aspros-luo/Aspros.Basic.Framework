using Grpc.AspNetCore.Server;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Framework helpers for registering and consuming gRPC services.
/// Framework 提供的 gRPC 服务端/客户端注册辅助方法。
///
/// <para>
/// The Framework wraps the standard ASP.NET Core gRPC APIs; it does not replace
/// protobuf-generated clients or service contracts owned by business services.
/// Framework 只是对 ASP.NET Core 原生 gRPC API 做统一封装，
/// 不会替业务服务生成或接管 proto / protobuf contract。
/// </para>
/// </summary>
public static class FrameworkGrpcExtensions
{
    /// <summary>
    /// Registers gRPC server support.
/// 注册 gRPC 服务端能力。
/// </summary>
    public static IServiceCollection AddFrameworkGrpc(
        this IServiceCollection services,
        Action<GrpcServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddGrpc(configure);
        return services;
    }

    /// <summary>
    /// Registers a typed gRPC client with its target address.
/// 注册一个强类型 gRPC Client，并指定目标服务地址。
/// </summary>
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

    /// <summary>
    /// Registers a named typed gRPC client.
/// 注册一个带名称的强类型 gRPC Client。
///
/// <para>
/// Use the named overload when the same client type needs multiple target endpoints.
/// 当同一种 Client 需要访问多个不同服务地址时，可以使用这个重载。
/// </para>
/// </summary>
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
/// 可选地把当前 HTTP Bearer Token 转发到下游 gRPC 调用。
///
/// <para>
/// Useful for API -> service -> service user-context propagation.
/// 适用于 API → Service → Service 场景下继续传递当前用户身份。
///
/// Only an already authenticated Bearer identity is forwarded.
/// 只有当前请求已经通过 Bearer Authentication 认证成功时才会转发 Token。
/// </para>
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

                if (httpContext is null ||
                    !httpContext.TryGetAuthenticatedBearerToken(out var token))
                {
                    return Task.CompletedTask;
                }

                metadata.Add(
                    "authorization",
                    $"Bearer {token}");

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Propagates the current gRPC deadline and cancellation context to a downstream gRPC call.
/// 将当前 gRPC 请求的 deadline 和 cancellation context 传播给下游 gRPC 调用。
///
/// <para>
/// This is useful for service -> service calls so a downstream call does not
/// outlive the original request unnecessarily.
/// 适用于 Service → Service 调用，避免下游请求无意义地超过上游请求生命周期。
/// </para>
///
/// <param name="suppressMissingContextErrors">
/// If true, do not throw when there is no active gRPC call context.
/// 如果为 true，当当前代码并不处于 gRPC 调用上下文时，不因为缺少 context 而抛异常。
/// </param>
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

    /// <summary>
    /// Maps a framework helper around ASP.NET Core's generated gRPC service endpoint.
/// 将业务的 protobuf-generated gRPC Service 注册到 ASP.NET Core Endpoint。
///
/// <para>
/// TService is the generated/implemented business gRPC service type.
/// TService 通常就是业务服务自己实现的 protobuf-generated Service 类型。
/// </para>
/// </summary>
    public static IEndpointConventionBuilder MapFrameworkGrpcService<TService>(
        this IEndpointRouteBuilder endpoints)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGrpcService<TService>();
    }
}
