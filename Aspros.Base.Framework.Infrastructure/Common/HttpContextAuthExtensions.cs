using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Authentication-related helpers for ASP.NET Core HttpContext.
/// ASP.NET Core HttpContext 的认证相关辅助方法。
/// </summary>
public static class HttpContextAuthExtensions
{
    /// <summary>
    /// Reads a Bearer token only when the current user has an authenticated Bearer identity.
    /// 只有当前用户存在已认证的 Bearer Identity 时，才读取 Authorization Bearer Token。
    ///
    /// <para>
    /// This prevents treating an arbitrary Authorization header as trusted identity data.
    /// 这样可以避免把任意 Authorization 请求头直接当成可信身份信息。
    /// </para>
    /// </summary>
    public static bool TryGetAuthenticatedBearerToken(
        this HttpContext context,
        out string token)
    {
        ArgumentNullException.ThrowIfNull(context);

        token = string.Empty;

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var bearerIdentity = context.User.Identities.Any(identity =>
            identity.IsAuthenticated &&
            string.Equals(
                identity.AuthenticationType,
                "Bearer",
                StringComparison.OrdinalIgnoreCase));

        if (!bearerIdentity)
        {
            return false;
        }

        var authorization = context.Request.Headers.Authorization.ToString();

        if (!AuthenticationHeaderValue.TryParse(
                authorization,
                out var header) ||
            !string.Equals(
                header.Scheme,
                "Bearer",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            return false;
        }

        token = header.Parameter;
        return true;
    }
}