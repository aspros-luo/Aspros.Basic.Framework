using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Security.Claims;

namespace Aspros.Base.Framework.Infrastructure;

public sealed class WorkContext(IHttpContextAccessor contextAccessor) : IWorkContext
{
    private readonly IHttpContextAccessor _contextAccessor =
        contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));

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

    public Task<long> GetTenantId() => Get<long>("tenant_id");

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
