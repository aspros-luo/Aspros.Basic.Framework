using Grpc.AspNetCore.Server;
using Grpc.Net.ClientFactory;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure.Grpc;

/// <summary>
/// Common gRPC registration helpers for microservice consumers.
/// Business contracts remain in consumer/shared contract projects.
/// </summary>
public static class GrpcServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core gRPC server services.
    /// </summary>
    public static IServiceCollection AddFrameworkGrpc(
        this IServiceCollection services,
        Action<GrpcServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return configure is null
            ? services.AddGrpc()
            : services.AddGrpc(configure);
    }

    /// <summary>
    /// Registers a generated gRPC client through the official gRPC client factory.
    /// </summary>
    public static IHttpClientBuilder AddFrameworkGrpcClient<TClient>(
        this IServiceCollection services,
        Uri address,
        Action<GrpcClientFactoryOptions>? configure = null)
        where TClient : ClientBase<TClient>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(address);
        ValidateAddress(address);

        return services.AddGrpcClient<TClient>(options =>
        {
            options.Address = address;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Registers a named generated gRPC client.
    /// </summary>
    public static IHttpClientBuilder AddFrameworkGrpcClient<TClient>(
        this IServiceCollection services,
        string name,
        Uri address,
        Action<GrpcClientFactoryOptions>? configure = null)
        where TClient : ClientBase<TClient>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);
        ValidateAddress(address);

        return services.AddGrpcClient<TClient>(name, options =>
        {
            options.Address = address;
            configure?.Invoke(options);
        });
    }

    private static void ValidateAddress(Uri address)
    {
        if (!address.IsAbsoluteUri ||
            (address.Scheme != Uri.UriSchemeHttp &&
             address.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "gRPC client address must be an absolute HTTP or HTTPS URI.",
                nameof(address));
        }
    }
}
