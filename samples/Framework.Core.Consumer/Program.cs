using Aspros.Base.Framework.Infrastructure;
using Framework.Core.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var endpoint = new Uri(
    builder.Configuration["Grpc:Provider"] ?? "https://localhost:7041");

builder.Services
    .AddFrameworkGrpcClient<CoreService.CoreServiceClient>(endpoint)
    .ForwardAuthorizationHeader();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "framework-core-consumer",
    status = "ok"
}));

app.MapControllers();

app.Run();

public partial class Program { }
