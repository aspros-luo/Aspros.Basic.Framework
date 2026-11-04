using Aspros.Base.Framework.Infrastructure.DependencyInjection;
using Aspros.Base.Framework.Infrastructure.Grpc;
using Aspros.Base.Framework.Infrastructure.WorkContext;
using Framework.Core.Provider.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. 注册 Aspros 基础微服务框架
builder.Services.AddAsprosFramework();

var app = builder.Build();

// 2. 注入工作上下文中间件
app.UseMiddleware<WorkContextMiddleware>();

// 3. 注册 gRPC 服务宿主
app.MapFrameworkGrpcService<CoreGrpcService>();

app.MapGet("/", () => "Aspros.Core.Provider (.NET 10 gRPC Service Running on https://localhost:7041)");

app.Run();
