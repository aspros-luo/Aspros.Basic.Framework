using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace Aspros.Base.Framework.Infrastructure;

public static class HttpContextAuthExtensions
{
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
