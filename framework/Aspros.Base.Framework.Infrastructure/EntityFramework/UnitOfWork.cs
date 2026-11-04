namespace Aspros.Base.Framework.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Aspros.Base.Framework.Domain.UoW;

/// <summary>
/// 生产级工作单元实现：
/// 1. RegisterNew / RegisterDirty 仅跟踪状态，绝不在方法内部偷偷执行 SaveChanges。
/// 2. 默认不强制嵌套物理事务；需要多个写操作原子落盘时调用 BeginTransactionAsync()。
/// 3. 与 DapperExecutor 共享当前物理连接与事务。
/// 4. 回滚时清理 ChangeTracker 避免脏实体污染后续操作。
/// </summary>
public sealed class UnitOfWork(IDbContext dbContext) : IUnitOfWork
{
    private readonly IDbContext _dbContext = dbContext;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public bool HasActiveTransaction => _currentTransaction != null || _dbContext.CurrentTransaction != null;

    public Task RegisterNewAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class
    {
        _dbContext.Set<TEntity>().Add(entity);
        return Task.CompletedTask;
    }

    public Task RegisterDirtyAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class
    {
        _dbContext.Set<TEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task RegisterCleanAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class
    {
        _dbContext.Set<TEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (HasActiveTransaction) return;
        _currentTransaction = await _dbContext.BeginTransactionAsync(cancellationToken);
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.SaveChangesAsync(cancellationToken);

        if (_currentTransaction != null)
        {
            await _currentTransaction.CommitAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        return rows;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        // 关键：清空当前上下文的追踪状态，防止已被修改的实体被后续提交误保存
        _dbContext.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _currentTransaction?.Dispose();
        _currentTransaction = null;
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
        _disposed = true;
    }
}
