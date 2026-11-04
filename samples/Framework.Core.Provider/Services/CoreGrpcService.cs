namespace Framework.Core.Provider.Services;

using Grpc.Core;
using Framework.Core.Contracts;
using Aspros.Base.Framework.WorkContext;

/// <summary>
/// 服务端 Core gRPC 服务实现
/// </summary>
public sealed class CoreGrpcService(IWorkContextAccessor workContextAccessor, ILogger<CoreGrpcService> logger) : CoreService.CoreServiceBase
{
    private readonly IWorkContextAccessor _workContextAccessor = workContextAccessor;
    private readonly ILogger<CoreGrpcService> _logger = logger;

    public override Task<CorePingResponse> Ping(CorePingRequest request, ServerCallContext context)
    {
        var tenantId = _workContextAccessor.Current.TenantId ?? request.TenantId;
        var callerZone = _workContextAccessor.Current.CallerZone ?? request.CallerZone;

        _logger.LogInformation("【gRPC Provider】收到来自可用区 {Zone}，租户 {TenantId} 的 Ping: {Message}", callerZone, tenantId, request.Message);

        return Task.FromResult(new CorePingResponse
        {
            EchoMessage = $"[Provider-Echo] {request.Message}",
            TenantId = tenantId,
            ServerZone = "zone-east-1a",
            ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TraceId = context.RequestHeaders.FirstOrDefault(h => h.Key == "x-trace-id")?.Value ?? Guid.NewGuid().ToString("N")
        });
    }

    public override Task<TradeStatusResponse> QueryTradeStatus(TradeStatusRequest request, ServerCallContext context)
    {
        return Task.FromResult(new TradeStatusResponse
        {
            OrderId = request.OrderId,
            Status = "Paid",
            TotalAmount = 9600.0,
            IsIdempotentProtected = true
        });
    }
}
