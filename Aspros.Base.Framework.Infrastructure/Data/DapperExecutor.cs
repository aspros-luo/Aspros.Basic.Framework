using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Executes explicit SQL through the current UnitOfWork connection.
/// 使用当前 UnitOfWork 的数据库连接执行显式 SQL。
///
/// <para>
/// Use this only when normal EF Core/Repository access is not a good fit,
/// for example complex hand-written SQL or read-model queries.
/// 仅在普通 EF Core/Repository 不适合时使用，例如复杂手写 SQL 或查询模型。
/// </para>
/// </summary>
public interface IDapperExecutor : IScoped
{
    /// <summary>
    /// Executes a query and returns all matching rows.
/// 执行查询并返回全部结果。
/// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and returns one row or null.
/// 执行查询并返回一条记录；没有记录时返回 null。
/// </summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes INSERT/UPDATE/DELETE or another command and returns affected rows.
/// 执行 INSERT/UPDATE/DELETE 等命令并返回受影响的行数。
/// </summary>
    Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin Dapper adapter over the same connection/transaction used by the current
/// unit of work.
/// 当前 UnitOfWork 的轻量 Dapper 适配器，复用同一个数据库连接/事务。
///
/// <para>
/// EF remains the default persistence path; this adapter is for explicit SQL/read-model/performance cases.
/// EF 仍然是默认持久化方案；这个适配器只用于明确需要 SQL、查询模型或性能优化的场景。
/// </para>
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

    /// <summary>
    /// Creates a Dapper command using the UnitOfWork's current transaction.
/// 使用 UnitOfWork 当前事务创建 Dapper Command。
///
/// <para>
/// This is important when EF and Dapper operations must participate in the
/// same database transaction.
/// 当 EF 和 Dapper 操作必须处于同一个数据库事务时，这一点非常重要。
/// </para>
/// </summary>
    private CommandDefinition CreateCommand(
        string sql,
        object? parameters,
        CancellationToken cancellationToken)
    {
        IDbTransaction? transaction =
            _unitOfWork.DbContextTransaction?.GetDbTransaction();

        return new CommandDefinition(
            sql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
    }
}