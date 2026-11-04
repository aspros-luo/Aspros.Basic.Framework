namespace Aspros.Base.Framework.Infrastructure.WorkContext;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Aspros.Base.Framework.WorkContext;

/// <summary>
/// 基于 AsyncLocal 的工作上下文访问器，保障协程与异步流间严格隔离
/// </summary>
public sealed class AsyncLocalWorkContextAccessor : IWorkContextAccessor
{
    private static readonly AsyncLocal<IWorkContext?> _currentContext = new();

    public IWorkContext Current => _currentContext.Value ?? WorkContextRecord.Empty;

    public void SetCurrent(IWorkContext context)
    {
        _currentContext.Value = context;
    }
}

/// <summary>
/// 工作上下文作用域工厂 (供 CAP MQ 消费者、后台定时任务、内部批处理派生上下文)
/// </summary>
public static class WorkContextScopeFactory
{
    public static IDisposable CreateScope(IWorkContext context, IWorkContextAccessor accessor)
    {
        var previous = accessor.Current;
        accessor.SetCurrent(context);
        return new WorkContextScope(previous, accessor);
    }

    private sealed class WorkContextScope(IWorkContext previous, IWorkContextAccessor accessor) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            accessor.SetCurrent(previous);
            _disposed = true;
        }
    }
}

/// <summary>
/// HTTP 边界工作上下文解析与注入中间件
/// </summary>
public sealed class WorkContextMiddleware(RequestDelegate next, IWorkContextAccessor accessor)
{
    private readonly RequestDelegate _next = next;
    private readonly IWorkContextAccessor _accessor = accessor;

    public async Task InvokeAsync(HttpContext context)
    {
        long? tenantId = null;

        // 1. 优先从 Header 读取 x-tenant-id
        if (context.Request.Headers.TryGetValue("x-tenant-id", out var tenantVal) &&
            long.TryParse(tenantVal, out var tid))
        {
            tenantId = tid;
        }

        // 2. 从已认证 Claims 读取 sub / user_id 与 tenant_id
        string? userId = null;
        string? userName = null;
        if (context.User.Identity?.IsAuthenticated == true)
        {
            userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                     context.User.FindFirstValue("sub") ??
                     context.User.FindFirstValue("user_id");

            userName = context.User.FindFirstValue(ClaimTypes.Name) ??
                       context.User.FindFirstValue("preferred_username");

            if (!tenantId.HasValue &&
                context.User.FindFirstValue("tenant_id") is { Length: > 0 } claimTenantStr &&
                long.TryParse(claimTenantStr, out var cTid))
            {
                tenantId = cTid;
            }
        }

        // 3. 读取 Authorization Bearer Token
        string? bearerToken = null;
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            bearerToken = authHeader.ToString();
        }

        // 4. 读取可用区
        string? callerZone = context.Request.Headers.TryGetValue("x-caller-zone", out var zoneVal)
            ? zoneVal.ToString()
            : null;

        var record = new WorkContextRecord
        {
            TenantId = tenantId,
            UserId = userId,
            UserName = userName,
            TraceId = context.TraceIdentifier,
            CorrelationId = context.Request.Headers["x-correlation-id"].ToString() ?? Guid.NewGuid().ToString("N"),
            CallerZone = callerZone,
            BearerToken = bearerToken
        };

        _accessor.SetCurrent(record);

        await _next(context);
    }
}
