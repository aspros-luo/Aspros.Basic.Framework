using Aspros.Base.Framework.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register the framework's common runtime services.
// 注册 Framework 的基础运行时能力。
builder.Services.AddAsprosFramework();

// gRPC hosting is an explicit opt-in integration.
// gRPC 宿主能力按需显式启用，不由基础注册自动加载。
builder.Services.AddFrameworkGrpc();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "framework-core-provider",
    status = "ok"
}));

// Map the business-owned generated protobuf service into ASP.NET Core.
// 将业务自己维护 proto 生成的 gRPC Service 映射到 ASP.NET Core。
app.MapFrameworkGrpcService<CoreGrpcService>();

app.Run();