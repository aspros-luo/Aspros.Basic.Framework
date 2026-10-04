using Grpc.AspNetCore.Server;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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

    public static IEndpointConventionBuilder MapFrameworkGrpcService<TService>(
        this IEndpointRouteBuilder endpoints)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGrpcService<TService>();
    }
}
