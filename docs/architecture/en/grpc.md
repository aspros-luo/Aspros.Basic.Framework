# gRPC Microservice Communication

## Positioning

Framework v10 treats gRPC as an internal microservice infrastructure capability, but it does not own business RPC contracts.

Framework provides the repeated infrastructure wiring:
- ASP.NET Core gRPC server registration
- Typed gRPC client registration
- Named clients
- A configuration convention for service endpoints
- Access to the official client-factory extension points for interceptors, call credentials, channel settings, deadlines and cancellation

Business services own their .proto contracts, generated clients/services, application ports, and domain semantics.

## Recommended shape

User / Category / Trade
        ↓
Shared *.proto contracts
        ↓
Generated gRPC client / server
        ↓
Framework gRPC registration
        ↓
Application business port
        ↓
Domain

## Why Infrastructure

gRPC is a transport/infrastructure capability.

Domain should not know about gRPC, HTTP/2, protobuf, ClientBase, ServerCallContext, Nacos, or network addresses.

If Application needs a synchronous cross-service call, define a small business port such as IUserProfileReader. Infrastructure can implement that port with the generated gRPC client.

## Client registration

Direct endpoint:

builder.Services.AddFrameworkGrpcClient<UserService.UserServiceClient>(
    new Uri("https://user.internal:5001"));

Configuration:

Grpc:Services:User:Address = https://user.internal:5001

builder.Services.AddFrameworkGrpcClient<UserService.UserServiceClient>(
    builder.Configuration,
    "User");

## Server registration

builder.Services.AddFrameworkGrpc();

var app = builder.Build();
app.MapGrpcService<UserGrpcService>();

gRPC services can coexist with existing ASP.NET Core Controllers.

## Contract strategy

Use protobuf-first for the shared service contract.

Microsoft documents code-first as a good fit when the whole system is .NET, while polyglot systems should prefer interoperable .proto contracts. citeturn146956search1

Framework must not contain User / Category / Trade business protobuf definitions.

## gRPC vs MQ

Use gRPC for synchronous internal calls that need an immediate response.

Use Integration Event + CAP Outbox + MQ for eventual consistency, event propagation, high-throughput asynchronous processing, retries, and decoupled workflows.

Do not turn every service relationship into synchronous gRPC just because the system is microservice-based.

## Service discovery

Framework does not embed Nacos Discovery.

A consumer that already uses Nacos can adapt discovered service endpoints into its gRPC client configuration. A Framework-level discovery adapter should only be added after multiple consumers demonstrate a stable repeated implementation.

## Principle

Framework standardizes the repeated gRPC infrastructure. Consumers own the business RPC semantics.