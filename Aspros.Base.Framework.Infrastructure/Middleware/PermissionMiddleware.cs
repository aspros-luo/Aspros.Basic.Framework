// Temporarily disabled: this legacy compatibility wrapper is coupled to the
// consuming SaaS project namespace and inherits from the sealed framework
// middleware. Keep the framework-native permission middleware independent.
//
// Re-enable only after the compatibility API has been redesigned without
// inheriting from FrameworkPermissionMiddleware.
//
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Http.Features;
//
// namespace Aspros.SaaS.System.Infrastructure;
//
// /// <summary>
// /// Legacy compatibility wrapper. New applications should use
// /// UseFrameworkPermissionValidation().
// /// </summary>
// public sealed class PermissionMiddleware(
//     RequestDelegate next)
//     : Aspros.Base.Framework.Infrastructure.FrameworkPermissionMiddleware(next)
// {
//     public static Endpoint? GetEndpoint(HttpContext context)
//     {
//         ArgumentNullException.ThrowIfNull(context);
//         return context.Features.Get<IEndpointFeature>()?.Endpoint;
//     }
// }
//
// public static class PermissionExtensions
// {
//     public static IApplicationBuilder UsePermissionValid(
//         this IApplicationBuilder builder)
//     {
//         ArgumentNullException.ThrowIfNull(builder);
//         return builder.UseMiddleware<PermissionMiddleware>();
//     }
// }
//