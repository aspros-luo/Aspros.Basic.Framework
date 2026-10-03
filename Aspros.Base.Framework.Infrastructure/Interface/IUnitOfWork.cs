using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure
{
    /// <summary>
    /// Legacy compatibility Unit of Work contract.
    /// Extends the Application persistence contract with legacy EF access and explicit transaction members.
    /// </summary>
    public interface IUnitOfWork : Application.Abstractions.Persistence.IUnitOfWork, IScoped
    {
        IDbContext DbContext { get; }
        DatabaseFacade Database { get; }
        IDbConnection Connection { get; }
        IDbContextTransaction BeginTransaction(IDbContextTransaction? dbContextTransaction = null);
        IDbContextTransaction? DbContextTransaction { get; set; }

        Task<int> ExecuteSqlCommandAsync(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters);

        void Rollback();
    }
}