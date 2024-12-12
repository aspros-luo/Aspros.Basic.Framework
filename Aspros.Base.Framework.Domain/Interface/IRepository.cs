namespace Aspros.Base.Framework.Domain
{
    public interface IRepository<out TAggregateRoot> where TAggregateRoot : class, IAggregateRoot
    {
        IQueryable<TAggregateRoot> GetAll();
    }
}
