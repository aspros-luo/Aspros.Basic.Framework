using Grpc.AspNetCore.Server;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Framework-level gRPC registration helpers.
/// The generated client/service types remain owned by each business service;
/// the framework only centralizes hosting and HttpClientFactory integration.
/// </summary>
public static class FrameworkGrpcExtensions
{
    /// <summary>
    /// Enables ASP.NET Core gRPC hosting.
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
    /// Registers a typed generated gRPC client with HttpClientFactory.
    /// </summary>
    public static IHttpClientBuilder AddFrameworkGrpcClient<TClient>(
        this IServiceCollection services,
        Uri address)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(address);

        return services.AddGrpcClient<TClient>(options => options.Address = address);
    }

    /// <summary>
    /// Registers a named typed generated gRPC client.
    /// </summary>
    public static IHttpClientBuilder AddFrameworkGrpcClient<TClient>(
        this IServiceCollection services,
        string name,
        Uri address)
        where TClient : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);

        return services.AddGrpcClient<TClient>(
            name,
            options => options.Address = address);
    }

    /// <summary>
    /// Maps a generated ASP.NET Core gRPC service.
    /// </summary>
    public static IEndpointConventionBuilder MapFrameworkGrpcService<TService>(
        this IEndpointRouteBuilder endpoints)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGrpcService<TService>();
    }
}
