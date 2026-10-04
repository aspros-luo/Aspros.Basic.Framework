namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Marks a service for transient dependency injection.
/// Kept in the historical namespace for source/binary compatibility.
/// </summary>
public interface ITransient
{
}

/// <summary>
/// Marks a service for scoped dependency injection.
/// Kept in the historical namespace for source/binary compatibility.
/// </summary>
public interface IScoped
{
}

/// <summary>
/// Marks a service for singleton dependency injection.
/// Kept in the historical namespace for source/binary compatibility.
/// </summary>
public interface ISingleton
{
}

/// <summary>
/// Marker for an optional in-process application/domain event.
/// This contract does not imply distributed delivery or durability.
/// </summary>
public interface IEvent
{
}

/// <summary>
/// Handles an optional in-process event.
/// </summary>
public interface IEventHandler<in T> where T : IEvent
{
    Task HandleAsync(T @event);
}
