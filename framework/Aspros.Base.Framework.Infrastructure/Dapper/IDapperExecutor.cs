namespace Aspros.Base.Framework.Infrastructure.Dapper;

using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using Aspros.Base.Framework.Infrastructure.EntityFramework;

/// <summary>
/// 复杂 SQL、统计报表与极限性能只读执行器接口
/// </summary>
public interface IDapperExecutor
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);
    Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default);
    Task<T> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dapper 统一执行器：自动共享当前 UnitOfWork 与 DbContext 的活跃连接与物理事务
/// </summary>
public sealed class DapperExecutor(IDbContext dbContext) : IDapperExecutor
{
    private readonly IDbContext _dbContext = dbContext;

    private DbConnection Connection => _dbContext.DatabaseConnection;

    private IDbTransaction? CurrentDbTransaction =>
        _dbContext.CurrentTransaction?.GetDbTransaction();

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        EnsureConnectionOpen();
        var cmd = new CommandDefinition(sql, param, transaction: CurrentDbTransaction, cancellationToken: cancellationToken);
        return await Connection.QueryAsync<T>(cmd);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        EnsureConnectionOpen();
        var cmd = new CommandDefinition(sql, param, transaction: CurrentDbTransaction, cancellationToken: cancellationToken);
        return await Connection.QueryFirstOrDefaultAsync<T>(cmd);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        EnsureConnectionOpen();
        var cmd = new CommandDefinition(sql, param, transaction: CurrentDbTransaction, cancellationToken: cancellationToken);
        return await Connection.ExecuteAsync(cmd);
    }

    public async Task<T> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        EnsureConnectionOpen();
        var cmd = new CommandDefinition(sql, param, transaction: CurrentDbTransaction, cancellationToken: cancellationToken);
        var result = await Connection.ExecuteScalarAsync<T>(cmd);
        return result!;
    }

    private void EnsureConnectionOpen()
    {
        if (Connection.State != ConnectionState.Open)
        {
            Connection.Open();
        }
    }
}
