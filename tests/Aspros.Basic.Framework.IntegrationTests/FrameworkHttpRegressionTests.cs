using Aspros.Base.Framework.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class FrameworkHttpRegressionTests
{
    [Fact]
    public async Task StandardResilience_RetriesTransientGet_AndDoesNotRetryUnsafePost()
    {
        var getHandler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var postHandler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        var services = new ServiceCollection();
        services
            .AddHttpClient("get")
            .ConfigurePrimaryHttpMessageHandler(() => getHandler)
            .UseFrameworkResilience();
        services
            .AddHttpClient("post")
            .ConfigurePrimaryHttpMessageHandler(() => postHandler)
            .UseFrameworkResilience();

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        using var getResponse = await factory.GetClient("get").GetAsync("http://framework.test/recover");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(2, getHandler.CallCount);

        using var postResponse = await factory.GetClient("post").PostAsync(
            "http://framework.test/write",
            new StringContent("payload"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, postResponse.StatusCode);
        Assert.Equal(1, postHandler.CallCount);
    }

    [Fact]
    public async Task HealthEndpoints_SeparateLivenessFromReadiness()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFrameworkHealthChecks(healthChecks =>
        {
            healthChecks.AddCheck(
                "business",
                () => HealthCheckResult.Unhealthy("dependency unavailable"),
                tags: ["ready"]);
        });

        var app = builder.Build();
        app.MapFrameworkHealthChecks();
        await app.StartAsync();

        var client = app.GetTestClient();

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task RateLimiting_Returns429AfterConfiguredPermitLimit()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFrameworkRateLimiting(options =>
        {
            options.AddFixedWindowLimiter(
                "framework-regression",
                limiter =>
                {
                    limiter.PermitLimit = 2;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.AutoReplenishment = false;
                });
        });

        var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/limited", () => Results.Ok("ok"))
            .RequireRateLimiting("framework-regression");

        await app.StartAsync();

        var client = app.GetTestClient();

        using var first = await client.GetAsync("/limited");
        using var second = await client.GetAsync("/limited");
        using var third = await client.GetAsync("/limited");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((HttpStatusCode)429, third.StatusCode);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task PermissionMiddleware_Returns403WhenPermissionServiceDenies()
    {
        await using var permissionServer = await PermissionStub.StartAsync("false");

        var builder = CreatePermissionTestBuilder(permissionServer.Endpoint);
        var app = builder.Build();

        app.UseRouting();
        app.UseFrameworkPermissionValidation();
        app.MapGet("/protected", () => Results.Ok("allowed"))
            .WithMetadata(new Permission("orders.read"));

        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task PermissionMiddleware_Returns503WhenPermissionServiceIsUnavailable()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsprosFramework();
        builder.Services.AddScoped<IWorkContext, TestWorkContext>();
        builder.Services.AddFrameworkPermissionValidation();
        builder.Services.AddSingleton<IServiceDiscovery, UnavailableServiceDiscovery>();

        var app = builder.Build();

        app.UseRouting();
        app.UseFrameworkPermissionValidation();
        app.MapGet("/protected", () => Results.Ok("allowed"))
            .WithMetadata(new Permission("orders.read"));

        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/protected");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static WebApplicationBuilder CreatePermissionTestBuilder(Uri permissionEndpoint)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsprosFramework();
        builder.Services.AddScoped<IWorkContext, TestWorkContext>();
        builder.Services.AddFrameworkPermissionValidation(options =>
        {
            options.ServiceName = "permission-regression";
            options.ValidationPath = "/";
        });
        builder.Services.AddSingleton<IServiceDiscovery>(
            new StaticServiceDiscovery(permissionEndpoint));
        return builder;
    }
}

internal sealed class SequenceHandler(params HttpStatusCode[] responses) : HttpMessageHandler
{
    private readonly IReadOnlyList<HttpStatusCode> _responses =
        responses.Length == 0
            ? throw new ArgumentException("At least one response is required.", nameof(responses))
            : responses;

    private int _callCount;

    public int CallCount => Volatile.Read(ref _callCount);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var index = Interlocked.Increment(ref _callCount) - 1;
        var status = _responses[Math.Min(index, _responses.Count - 1)];

        return Task.FromResult(new HttpResponseMessage(status)
        {
            RequestMessage = request
        });
    }
}

internal sealed class StaticServiceDiscovery(Uri endpoint) : IServiceDiscovery
{
    public Task<ServiceEndpoint?> GetHealthyEndpointAsync(
        string serviceName,
        string groupName = "DEFAULT_GROUP",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<ServiceEndpoint?>(
            new ServiceEndpoint(
                endpoint,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
    }
}

internal sealed class UnavailableServiceDiscovery : IServiceDiscovery
{
    public Task<ServiceEndpoint?> GetHealthyEndpointAsync(
        string serviceName,
        string groupName = "DEFAULT_GROUP",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ServiceEndpoint?>(null);
    }
}

internal sealed class TestWorkContext : IWorkContext
{
    public Task<long> GetUserId() => Task.FromResult(1L);
}

internal sealed class PermissionStub : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _serverTask;

    private PermissionStub(TcpListener listener, Uri endpoint, string body)
    {
        _listener = listener;
        Endpoint = endpoint;
        _serverTask = ServeAsync(body);
    }

    public Uri Endpoint { get; }

    public static Task<PermissionStub> StartAsync(string jsonBody)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var address = (IPEndPoint)listener.LocalEndpoint;
        var endpoint = new UriBuilder(
            Uri.UriSchemeHttp,
            IPAddress.Loopback.ToString(),
            address.Port,
            "/")
            .Uri;

        return Task.FromResult(new PermissionStub(listener, endpoint, jsonBody));
    }

    private async Task ServeAsync(string body)
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();

            var buffer = new byte[4096];
            var request = new StringBuilder();

            while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    return;
                }

                request.Append(Encoding.ASCII.GetString(buffer, 0, read));

                if (request.Length > 16 * 1024)
                {
                    return;
                }
            }

            var payload = Encoding.UTF8.GetBytes(body);
            var headers =
                $"HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json\r\n" +
                $"Content-Length: {payload.Length}\r\n" +
                "Connection: close\r\n\r\n";

            var response = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(response);
            await stream.WriteAsync(payload);
            await stream.FlushAsync();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        try
        {
            await _serverTask;
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
