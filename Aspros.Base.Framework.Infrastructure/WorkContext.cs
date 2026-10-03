using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// 基于 ASP.NET Core 已认证 ClaimsPrincipal 的工作上下文。
/// 不重新解析或信任未经验证的 Bearer Token，也不依赖 Redis 保存整份 token payload。
/// </summary>
public sealed class WorkContext(IHttpContextAccessor contextAccessor) : IWorkContext
{
    private readonly IHttpContextAccessor _contextAccessor = contextAccessor;

    public Task<long> GetUserId()
        => Task.FromResult(ParseLongClaim("user_id", ClaimTypes.NameIdentifier, "sub"));

    public Task<long> GetTenantId()
        => Task.FromResult(ParseLongClaim("tenant_id"));

    public Task<T> Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = GetClaimValue(key);
        if (value is null)
        {
            throw new KeyNotFoundException($"Claim '{key}' was not found for the current user.");
        }

        if (typeof(T) == typeof(string))
        {
            return Task.FromResult((T)(object)value);
        }

        var result = JsonSerializer.Deserialize<T>(value);
        if (result is null)
        {
            throw new InvalidOperationException($"Claim '{key}' could not be deserialized as {typeof(T).FullName}.");
        }

        return Task.FromResult(result);
    }

    private long ParseLongClaim(params string[] claimTypes)
    {
        var value = claimTypes
            .Select(GetClaimValue)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return value is not null && long.TryParse(value, out var result)
            ? result
            : 0;
    }

    private string? GetClaimValue(string claimType)
    {
        var user = _contextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst(claimType)?.Value;
    }
}
