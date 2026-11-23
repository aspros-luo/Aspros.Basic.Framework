using Aspros.Base.Framework.Infrastructure;
using Framework.Core.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsprosFramework();
builder.Services.AddControllers();

// The provider address belongs to the application configuration.
// Provider 地址由业务应用配置管理，而不是硬编码在 Framework 内部。
var endpoint = new Uri(
    builder.Configuration["Grpc:Provider"] ?? "https://localhost:7041");

builder.Services
    .AddFrameworkGrpcClient<CoreService.CoreServiceClient>(endpoint);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "framework-core-consumer",
    status = "ok"
}));

app.MapControllers();

app.Run();

public partial class Program { }