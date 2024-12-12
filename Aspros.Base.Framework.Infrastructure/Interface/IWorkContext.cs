namespace Aspros.Base.Framework.Infrastructure
{
    public interface IWorkContext : IScoped
    {
        Task<long> GetUserId();
        Task<long> GetTenantId();
        Task<T> Get<T>(string key);
    }
}
