using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Aspros.Base.Framework.Infrastructure;

public sealed class RewriteQueryStringMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next =
        next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Method is "POST" or "PUT")
        {
            await RewriteJsonBodyAsync(context, context.RequestAborted);
        }
        else if (HttpMethods.IsGet(context.Request.Method))
        {
            RewriteQuery(context);
        }

        await _next(context);
    }

    private static async Task RewriteJsonBodyAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var contentType = context.Request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType) ||
            !contentType.StartsWith(
                "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context.Request.ContentLength == 0)
        {
            return;
        }

        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        var requestBody = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return;
        }

        var dictionary =
            JsonConvert.DeserializeObject<Dictionary<string, JToken?>>(
                requestBody);

        if (dictionary is null)
        {
            return;
        }

        var normalized = new Dictionary<string, JToken?>(
            dictionary.Count,
            StringComparer.Ordinal);

        foreach (var pair in dictionary)
        {
            var key = pair.Key.ToPascalCase();
            normalized.Add(key, pair.Value);
        }

        var rewrittenBody = JsonConvert.SerializeObject(normalized);
        var bodyBytes = Encoding.UTF8.GetBytes(rewrittenBody);

        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        context.Request.Body.Position = 0;
    }

    private static void RewriteQuery(HttpContext context)
    {
        if (!context.Request.QueryString.HasValue)
        {
            return;
        }

        var pairs = new List<KeyValuePair<string, string>>();

        foreach (var pair in QueryHelpers.ParseQuery(
                     context.Request.QueryString.Value!))
        {
            var key = pair.Key.Replace("_", string.Empty, StringComparison.Ordinal);

            foreach (var value in pair.Value)
            {
                pairs.Add(new KeyValuePair<string, string>(
                    key,
                    value ?? string.Empty));
            }
        }

        context.Request.QueryString = QueryString.Create(pairs);
    }
}

public static class RewriteQueryStringExtensions
{
    public static IApplicationBuilder UseRewriteQueryString(
        this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseMiddleware<RewriteQueryStringMiddleware>();
    }
}
