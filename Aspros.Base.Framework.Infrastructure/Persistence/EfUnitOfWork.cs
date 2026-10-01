using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.Infrastructure.Persistence;

/// <summary>
/// 基于 EF Core DbContext 的 Unit of Work 实现。
/// Framework 不提供具体 DbContext，业务项目负责注册和配置自己的 DbContext。
/// </summary>
public sealed class EfUnitOfWork(DbContext dbContext) : IUnitOfWork
{
    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
