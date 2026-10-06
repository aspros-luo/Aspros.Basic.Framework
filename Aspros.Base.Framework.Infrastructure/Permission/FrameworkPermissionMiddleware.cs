using Aspros.Base.Framework.Infrastructure;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Validates endpoint permissions through the discovered permission service.
/// 通过服务发现找到权限服务，并对当前 Endpoint 执行权限校验。
///
/// <para>
/// The middleware is opt-in through the framework permission extension; endpoints
/// without the Permission metadata continue through the normal pipeline.
/// 该中间件通过 Framework 权限扩展按需启用；没有 Permission 元数据的 Endpoint
/// 会直接进入正常请求管道，不会额外调用权限服务。
/// </para>
/// </summary>
/// <param name="next">The next middleware in the ASP.NET Core pipeline. / ASP.NET Core 管道中的下一个中间件。</param>
