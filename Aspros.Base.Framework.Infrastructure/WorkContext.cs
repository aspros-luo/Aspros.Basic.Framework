using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Reads request-scoped identity data from the authenticated ClaimsPrincipal.
/// JWT payload decoding is used only as a compatibility fallback for custom claims.
/// </summary>
public sealed class WorkContext(IHttpContextAccessor contextAccessor) : IWorkContext
{
    private readonly IHttpContextAccessor _contextAccessor =
        contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));

    public async Task<T> Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryGetClaim(key, out var claimValue))
        {
            return ConvertClaimValue<T>(claimValue);
        }

        var tokenPayload = await GetTokenPayloadAsync();
        var value = tokenPayload?[key];

        return value is null || value.Type == JTokenType.Null
            ? default!
            : value.ToObject<T>()!;
    }

    public async Task<long> GetTenantId()
    {
        var value = await Get<long>("tenant_id");
        return value;
    }

    public async Task<long> GetUserId()
    {
        if (TryGetClaim("user_id", out var userId) ||
            TryGetClaim(ClaimTypes.NameIdentifier, out userId) ||
            TryGetClaim("sub", out userId))
        {
            return ConvertClaimValue<long>(userId);
        }

        var tokenPayload = await GetTokenPayloadAsync();
        return tokenPayload?["user_id"]?.ToObject<long>()
               ?? tokenPayload?["sub"]?.ToObject<long>()
               ?? 0L;
    }

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

    private async Task<JObject?> GetTokenPayloadAsync()
    {
        var httpContext = _contextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (!AuthenticationHeaderValue.TryParse(
                httpContext.Request.Headers.Authorization.ToString(),
                out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return null;
        }

        try
        {
            var payload = Jose.JWT.Payload(authorization.Parameter);
            return JsonConvert.DeserializeObject<JObject>(payload);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or JsonException)
        {
            return null;
        }
    }

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
