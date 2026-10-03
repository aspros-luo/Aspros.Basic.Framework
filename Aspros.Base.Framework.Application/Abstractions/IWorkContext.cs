namespace Aspros.Base.Framework.Application.Abstractions;

/// <summary>
/// 当前请求/当前用户上下文。
/// Application 只依赖抽象，不依赖 ASP.NET、JWT 或 Redis 实现细节。
/// </summary>
public interface IWorkContext
{
    Task<long> GetUserId();
    Task<long> GetTenantId();
    Task<T> Get<T>(string key);
}
