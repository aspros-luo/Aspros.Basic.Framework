using Aspros.Base.Framework.Infrastructure.DependencyInjection;
using Aspros.Base.Framework.Infrastructure.Grpc;
using Aspros.Base.Framework.Infrastructure.WorkContext;
using Framework.Core.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 1. 注册 Aspros 基础微服务框架
builder.Services.AddAsprosFramework();

// 2. 注册 gRPC Client 并开启 Bearer Token 与链路上下文级联透传
builder.Services
    .AddFrameworkGrpcClient<CoreService.CoreServiceClient>(new Uri("https://localhost:7041"))
    .ForwardAuthorizationHeader();

var app = builder.Build();

// 3. 注入工作上下文中间件
app.UseMiddleware<WorkContextMiddleware>();

app.MapControllers();

app.MapGet("/", () => "Aspros.Core.Consumer (.NET 10 Web API Running on https://localhost:7141)");

app.Run();
