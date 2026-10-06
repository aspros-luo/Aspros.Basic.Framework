using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Security.Claims;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Reads current-request identity and tenant information.
/// 读取当前 HTTP 请求中的用户身份和租户信息。
///
/// <para>
/// Claims are preferred. JWT payload parsing is only a compatibility fallback for
/// authenticated Bearer requests.
/// 优先使用 Claims；只有在已认证 Bearer 请求下，JWT Payload 才作为兼容性兜底方案。
/// </para>
/// </summary>
public sealed class WorkContext(IHttpContextAccessor contextAccessor) : IWorkContext
{
    private readonly IHttpContextAccessor _contextAccessor =
        contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));

    /// <summary>
    /// Gets a value from claims first, then from the authenticated Bearer JWT payload.
    /// 优先从 Claims 获取值，找不到时再从已认证 Bearer JWT Payload 中读取。
    ///
    /// <typeparam name="T">Target value type. / 目标值类型。</typeparam>
    /// </summary>
    public Task<T> Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryGetClaim(key, out var claimValue))
        {
            return Task.FromResult(ConvertClaimValue<T>(claimValue));
        }

        var tokenPayload = GetTokenPayload();
        var value = tokenPayload?[key];

        return Task.FromResult(
            value is null || value.Type == JTokenType.Null
                ? default!
                : value.ToObject<T>()!);
    }

    /// <summary>
    /// Gets the current tenant id.
    /// 获取当前租户 ID。
    /// </summary>
    public Task<long> GetTenantId() => Get<long>("tenant_id");

    /// <summary>
    /// Gets the current user id from common claim names.
    /// 从常见 Claim 名称中获取当前用户 ID。
    /// </summary>
    public Task<long> GetUserId()
    {
        if (TryGetClaim("user_id", out var userId) ||
            TryGetClaim(ClaimTypes.NameIdentifier, out userId) ||
            TryGetClaim("sub", out userId))
        {
            return Task.FromResult(ConvertClaimValue<long>(userId));
        }

        var tokenPayload = GetTokenPayload();

        return Task.FromResult(
            tokenPayload?["user_id"]?.ToObject<long>()
            ?? tokenPayload?["sub"]?.ToObject<long>()
            ?? 0L);
    }

    /// <summary>
    /// Finds a non-empty authenticated claim using an exact claim-type match.
    /// 使用精确的 Claim Type 匹配查找已认证用户中的非空 Claim。
    /// </summary>
    private bool TryGetClaim(string key, out string value)
    {
        value = string.Empty;

        var user = _contextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var claim = user.Claims.FirstOrDefault(
            x => x.Type.Equals(key, StringComparison.Ordinal));

        if (claim is null || string.IsNullOrWhiteSpace(claim.Value))
        {
            return false;
        }

        value = claim.Value;
        return true;
    }

    /// <summary>
    /// Parses the JWT payload only after the request has passed Bearer authentication checks.
    /// 只有请求通过 Bearer 认证检查后，才解析 JWT Payload。
    /// </summary>
    private JObject? GetTokenPayload()
    {
        var httpContext = _contextAccessor.HttpContext;

        if (httpContext is null ||
            !httpContext.TryGetAuthenticatedBearerToken(out var token))
        {
            return null;
        }

        try
        {
            var payload = Jose.JWT.Payload(token);
            return JsonConvert.DeserializeObject<JObject>(payload);
        }
        catch (Exception ex)
            when (ex is FormatException or ArgumentException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a string claim into the requested target type.
    /// 将字符串形式的 Claim 转换为调用方要求的目标类型。
    ///
    /// <para>
    /// String values are returned directly; other types first use JSON conversion
    /// and then fall back to Convert.ChangeType for simple scalar values.
    /// 字符串直接返回；其他类型先尝试 JSON 转换，再对简单标量类型使用 Convert.ChangeType 兜底。
    /// </para>
    /// </summary>
    private static T ConvertClaimValue<T>(string value)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)value;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(value)
                   ?? throw new InvalidOperationException(
                       $"Unable to convert claim '{value}' to {typeof(T).Name}.");
        }
        catch (JsonException)
        {
            return (T)Convert.ChangeType(
                value,
                Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T),
                CultureInfo.InvariantCulture);
        }
    }
}