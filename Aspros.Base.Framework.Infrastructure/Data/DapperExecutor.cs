using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure;

public interface IDapperExecutor : IScoped
{
    Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin Dapper adapter over the same connection/transaction used by the current
/// unit of work. EF remains the default persistence path; this adapter is for
/// explicit SQL/read-model/performance cases.
/// </summary>
public sealed class DapperExecutor(IUnitOfWork unitOfWork) : IDapperExecutor
{
    private readonly IUnitOfWork _unitOfWork =
        unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _unitOfWork.Connection.QueryAsync<T>(
            CreateCommand(sql, parameters, cancellationToken));
    }

    public Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _unitOfWork.Connection.QuerySingleOrDefaultAsync<T>(
            CreateCommand(sql, parameters, cancellationToken));
    }

    public Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _unitOfWork.Connection.ExecuteAsync(
            CreateCommand(sql, parameters, cancellationToken));
    }

    private CommandDefinition CreateCommand(
        string sql,
        object? parameters,
        CancellationToken cancellationToken)
    {
        IDbTransaction? transaction = _unitOfWork.DbContextTransaction?.GetDbTransaction();

        return new CommandDefinition(
            sql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
    }
}
