namespace Aspros.Base.Framework.Application.Abstractions.Messaging;

/// <summary>
/// 应用层集成事件发布抽象。
/// Application 不直接依赖具体消息队列或 Outbox 实现。
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(
        string name,
        TEvent eventData,
        CancellationToken cancellationToken = default);
}
