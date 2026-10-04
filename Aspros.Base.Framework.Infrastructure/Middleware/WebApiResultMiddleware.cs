using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace Aspros.Base.Framework.Infrastructure;

public static class HttpContextExtensions
{
    public static string GetHeader(
        this HttpContext? httpContext,
        string key)
    {
        if (httpContext is null ||
            string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return httpContext.Request.Headers.TryGetValue(key, out var values)
            ? values.ToString()
            : string.Empty;
    }
}

public sealed class WebApiResultMiddleware : ActionFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = context.HttpContext.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (path.Contains("/inside.", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/health", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/open.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context.Result is FileContentResult or EmptyResult)
        {
            return;
        }

        if (context.Result is not ObjectResult objectResult)
        {
            return;
        }

        // Do not rewrite error payloads such as ProblemDetails.
        if (objectResult.StatusCode is >= 300)
        {
            return;
        }

        // Preserve an already wrapped response produced by WebApiController
        // or another application-level response wrapper.
        if (IsAlreadyWrapped(objectResult.Value))
        {
            return;
        }

        var settings = new JsonSerializerSettings
        {
            DateFormatString = "yyyy-MM-dd HH:mm",
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        context.Result = new JsonResult(
            new { data = objectResult.Value },
            settings)
        {
            StatusCode = objectResult.StatusCode,
            ContentType = objectResult.ContentTypes.FirstOrDefault()
        };
    }

    private static bool IsAlreadyWrapped(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();

        var success = type.GetProperty(
            "is_success",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        var data = type.GetProperty(
            "data",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        return success is not null && data is not null;
    }
}

public sealed class NullToEmptyStringResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(
        Type type,
        MemberSerialization memberSerialization)
    {
        return type
            .GetProperties()
            .Select(property =>
            {
                var jsonProperty = base.CreateProperty(
                    property,
                    memberSerialization);

                jsonProperty.ValueProvider =
                    new NullToEmptyStringValueProvider(property);

                return jsonProperty;
            })
            .ToList();
    }
}

public sealed class NullToEmptyStringValueProvider(PropertyInfo memberInfo)
    : IValueProvider
{
    private readonly PropertyInfo _memberInfo =
        memberInfo ?? throw new ArgumentNullException(nameof(memberInfo));

    public object? GetValue(object target)
    {
        return _memberInfo.GetValue(target) ?? string.Empty;
    }

    public void SetValue(object target, object? value)
    {
        _memberInfo.SetValue(target, value);
    }
}
