using Framework.Core.Grpc;
using Grpc.Core;

public sealed class CoreGrpcService : CoreService.CoreServiceBase
{
    public override Task<PingReply> Ping(
        PingRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message)
                ? "pong"
                : $"pong: {request.Message}",
            Service = "framework-core-provider",
            UnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }
}
