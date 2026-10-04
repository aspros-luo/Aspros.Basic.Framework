# gRPC 微服务通信

## 定位

Framework v10 将 gRPC 视为微服务基础设施能力之一，但不把业务 RPC Contract 做进 Framework。

Framework 负责重复性的基础设施接入：
- ASP.NET Core gRPC Server 注册
- Typed Client 注册
- Named Client
- 基于配置的服务地址约定
- 保留官方 Client Factory 的 Interceptor、Call Credentials、Channel、Deadline/Cancellation 等扩展能力

业务服务负责：
- .proto Contract
- Generated Client / Server
- Application 层业务接口
- 用户、分类、交易等具体领域语义

## 推荐结构

User / Category / Trade
        ↓
Shared *.proto contracts
        ↓
Generated gRPC clients / services
        ↓
Framework gRPC registration
        ↓
Application business port
        ↓
Domain

## 为什么放在 Infrastructure

gRPC 是 Transport / Infrastructure 能力。

Domain 不应该知道 gRPC、HTTP/2、protobuf、ClientBase、ServerCallContext、Nacos 或网络地址。

Application 如果确实需要同步跨服务调用，可以定义自己的最小业务 Port：

public interface IUserProfileReader
{
    Task<UserProfile> GetAsync(long userId, CancellationToken cancellationToken = default);
}

Infrastructure 再使用生成的 gRPC Client 实现。

## Client 注册

最简单的配置：

builder.Services.AddFrameworkGrpcClient<UserService.UserServiceClient>(
    new Uri("https://user.internal:5001"));

推荐使用配置：

{
  "Grpc": {
    "Services": {
      "User": { "Address": "https://user.internal:5001" },
      "Category": { "Address": "https://category.internal:5001" },
      "Trade": { "Address": "https://trade.internal:5001" }
    }
  }
}

builder.Services.AddFrameworkGrpcClient<UserService.UserServiceClient>(
    builder.Configuration,
    "User");

## Server 注册

builder.Services.AddFrameworkGrpc();

var app = builder.Build();
app.MapGrpcService<UserGrpcService>();

gRPC 服务可以与现有 ASP.NET Core Controller 同时存在。

## Contract 策略

推荐 protobuf-first。

原因是微服务最终可能存在 Java 等非 .NET 消费者。Microsoft 的 .NET gRPC 文档也指出，code-first 更适合整个系统都使用 .NET 的场景，而 polyglot 系统更适合共享 .proto Contract。citeturn146956search1

Framework 不保存 User、Category、Trade 的业务 Contract。

## gRPC 与消息队列的边界

gRPC 适合需要即时响应的同步内部调用。

MQ / Integration Event 适合最终一致性、事件传播、高吞吐和 Outbox / Retry。

不要为了让所有微服务都使用 gRPC，而把所有跨服务关系强制改成同步调用。

## Service Discovery

Framework 不内置 Nacos Discovery。

服务地址通过普通配置即可工作；消费者如果已经使用 Nacos，可以由消费者 Infrastructure 负责把服务发现结果转换为 gRPC Client 配置。

只有当 User / Category / Trade 出现稳定、重复的动态服务发现适配代码后，再考虑提升为 Framework 级能力。

## 当前原则

Framework 封装 gRPC 的重复基础设施，不封装 User / Category / Trade 的业务语义。

不要在 Framework 中定义一个让业务通过字符串调用远端服务的通用 IRpcClient。