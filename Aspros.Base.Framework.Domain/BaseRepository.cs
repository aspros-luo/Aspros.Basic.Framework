using Aspros.Base.Framework.Infrastructure;

namespace Aspros.Base.Framework.Domain
{
    public abstract class BaseRepository<TAggregateRoot> : IRepository<TAggregateRoot> where TAggregateRoot : class, IAggregateRoot
    {
        public readonly IQueryable<TAggregateRoot> Entities;

        protected BaseRepository(IDbContext dbContext) => Entities = dbContext.Set<TAggregateRoot>();

        public IQueryable<TAggregateRoot> GetAll()
        {
            return Entities;
        }

    }

}
