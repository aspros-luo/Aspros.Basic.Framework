namespace Aspros.Base.Framework.Infrastructure.Grpc;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Aspros.Base.Framework.WorkContext;

/// <summary>
/// gRPC 客户端链路元数据透传拦截器
/// </summary>
public sealed class GrpcHeaderPropagationInterceptor(IWorkContextAccessor accessor) : Interceptor
{
    private readonly IWorkContextAccessor _accessor = accessor;

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        var current = _accessor.Current;

        // 1. 透传 Bearer Token
        if (!string.IsNullOrEmpty(current.BearerToken) && !headers.Any(h => h.Key == "authorization"))
        {
            headers.Add("authorization", current.BearerToken);
        }

        // 2. 透传多租户标识
        if (current.TenantId.HasValue && !headers.Any(h => h.Key == "x-tenant-id"))
        {
            headers.Add("x-tenant-id", current.TenantId.Value.ToString());
        }

        // 3. 透传链路跟踪与关联标识
        if (!string.IsNullOrEmpty(current.TraceId) && !headers.Any(h => h.Key == "x-trace-id"))
        {
            headers.Add("x-trace-id", current.TraceId);
        }
        if (!string.IsNullOrEmpty(current.CorrelationId) && !headers.Any(h => h.Key == "x-correlation-id"))
        {
            headers.Add("x-correlation-id", current.CorrelationId);
        }

        // 4. 透传调用方可用区
        if (!string.IsNullOrEmpty(current.CallerZone) && !headers.Any(h => h.Key == "x-caller-zone"))
        {
            headers.Add("x-caller-zone", current.CallerZone);
        }

        var newOptions = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);

        return continuation(request, newContext);
    }
}

/// <summary>
/// gRPC 基础设施与扩展注册
/// </summary>
public static class GrpcFrameworkExtensions
{
    public static IServiceCollection AddFrameworkGrpc(this IServiceCollection services)
    {
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16MB
        });
        services.AddSingleton<GrpcHeaderPropagationInterceptor>();
        return services;
    }

    public static IHttpClientBuilder AddFrameworkGrpcClient<TClient>(this IServiceCollection services, Uri baseAddress)
        where TClient : ClientBase<TClient>
    {
        return services.AddGrpcClient<TClient>(o =>
        {
            o.Address = baseAddress;
        }).AddInterceptor<GrpcHeaderPropagationInterceptor>();
    }

    public static IHttpClientBuilder ForwardAuthorizationHeader(this IHttpClientBuilder builder)
    {
        return builder.AddInterceptor<GrpcHeaderPropagationInterceptor>();
    }

    public static GrpcServiceEndpointConventionBuilder MapFrameworkGrpcService<TService>(this IEndpointRouteBuilder endpoints)
        where TService : class
    {
        return endpoints.MapGrpcService<TService>();
    }
}
