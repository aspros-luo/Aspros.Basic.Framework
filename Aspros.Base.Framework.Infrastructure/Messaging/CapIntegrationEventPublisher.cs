using Aspros.Base.Framework.Application.Abstractions.Messaging;
using DotNetCore.CAP;

namespace Aspros.Base.Framework.Infrastructure.Messaging;

/// <summary>
/// 基于 DotNetCore.CAP 的集成事件发布实现。
/// CAP 负责本地消息表和 Outbox 投递，具体 Transport 由业务项目配置。
/// </summary>
public sealed class CapIntegrationEventPublisher(ICapPublisher publisher)
    : IIntegrationEventPublisher
{
    public Task PublishAsync<TEvent>(
        string name,
        TEvent eventData,
        CancellationToken cancellationToken = default)
        => publisher.PublishAsync(name, eventData, cancellationToken: cancellationToken);
}
