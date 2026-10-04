using Aspros.Base.Framework.Infrastructure;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsprosFramework();
builder.Services.AddFrameworkGrpc();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "framework-core-provider",
    status = "ok"
}));

app.MapFrameworkGrpcService<CoreGrpcService>();

app.Run();
