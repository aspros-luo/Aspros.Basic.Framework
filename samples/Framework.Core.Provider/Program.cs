using Aspros.Base.Framework.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7041, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
});

builder.Services.AddFrameworkGrpc();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "framework-core-provider",
    status = "ok"
}));

app.MapFrameworkGrpcService<CoreGrpcService>();

app.Run();
