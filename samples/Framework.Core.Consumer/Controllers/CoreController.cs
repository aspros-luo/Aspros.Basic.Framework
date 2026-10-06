using Framework.Core.Grpc;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("core")]
public sealed class CoreController(CoreService.CoreServiceClient client) : ControllerBase
{
    /// <summary>
    /// Demonstrates a normal HTTP -> gRPC service-to-service call.
    /// 演示一个标准的 HTTP -> gRPC 服务间调用。
    /// </summary>
    [HttpGet("ping")]
    public async Task<IActionResult> Ping(
        [FromQuery] string message = "hello",
        CancellationToken cancellationToken = default)
    {
        var reply = await client.PingAsync(
            new PingRequest { Message = message },
            cancellationToken: cancellationToken);

        return Ok(new
        {
            reply.Message,
            reply.Service,
            reply.UnixTime
        });
    }
}