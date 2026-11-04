namespace Framework.Core.Consumer.Controllers;

using Microsoft.AspNetCore.Mvc;
using Framework.Core.Contracts;
using Aspros.Base.Framework.Results;
using Aspros.Base.Framework.WorkContext;

[ApiController]
[Route("core")]
public sealed class CorePingController(
    CoreService.CoreServiceClient client,
    IWorkContextAccessor workContextAccessor) : ControllerBase
{
    private readonly CoreService.CoreServiceClient _client = client;
    private readonly IWorkContextAccessor _workContextAccessor = workContextAccessor;

    /// <summary>
    /// 调用 Provider gRPC 服务并透传 Bearer Token、租户上下文与 Deadline
    /// </summary>
    [HttpGet("ping")]
    public async Task<ActionResult<ApiResult<CorePingResponse>>> Ping(
        [FromQuery] string message = "hello from consumer",
        CancellationToken cancellationToken = default)
    {
        var current = _workContextAccessor.Current;

        var request = new CorePingRequest
        {
            Message = message,
            TenantId = current.TenantId ?? 100,
            CallerZone = current.CallerZone ?? "zone-east-1a"
        };

        // 级联传递 500ms 超时 Deadline 上下文
        var response = await _client.PingAsync(
            request,
            deadline: DateTime.UtcNow.AddMilliseconds(500),
            cancellationToken: cancellationToken);

        return Ok(ApiResult<CorePingResponse>.Ok(response, "跨微服务 gRPC 调用成功并透传上下文"));
    }
}
