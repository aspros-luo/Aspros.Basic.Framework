using Aspros.Base.Framework.Application.Abstractions;
using Aspros.Base.Framework.Application.Abstractions.Events;
using Aspros.Base.Framework.Domain.Kernel;
using Aspros.Base.Framework.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspros.Base.Framework.Tests;

public sealed class FrameworkDddKernelTests
{
    [Fact]
    public void ValueObjects_of_different_types_are_not_equal()
    {
        var first = new EmailAddress("demo@example.com");
        var second = new CustomerCode("demo@example.com");

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
    }

    [Fact]
    public void ValueObjects_of_same_type_compare_by_components()
    {
        var first = new EmailAddress("demo@example.com");
        var second = new EmailAddress("demo@example.com");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Entities_compare_by_runtime_type_and_identity()
    {
        var first = new Customer(Guid.Empty);
        var second = new Customer(Guid.Empty);
        var differentType = new OtherCustomer(Guid.Empty);

        Assert.Equal(first, second);
        Assert.NotEqual(first, differentType);
    }

    [Fact]
    public void AggregateRoot_tracks_and_clears_domain_events()
    {
        var aggregate = new CustomerAggregate(Guid.NewGuid());

        aggregate.Registered();

        var events = Assert.IsAssignableFrom<IReadOnlyCollection<IDomainEvent>>(
            ((IAggregateRoot)aggregate).DomainEvents);

        Assert.Single(events);

        ((IAggregateRoot)aggregate).ClearDomainEvents(events);

        Assert.Empty(((IAggregateRoot)aggregate).DomainEvents);
    }

    [Fact]
    public async Task AutoInject_registers_and_dispatches_domain_event_handlers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<HandledEventState>();
        services.AutoInject(
            typeof(FrameworkDddKernelTests).Assembly,
            typeof(DomainEventDispatcher).Assembly);

        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync([new CustomerRegistered(Guid.NewGuid())]);

        var state = provider.GetRequiredService<HandledEventState>();
        Assert.Equal(1, state.Count);
    }

    [Fact]
    public async Task AutoInject_registers_command_handlers_through_mediatr()
    {
        var services = new ServiceCollection();
        services.AutoInject(typeof(FrameworkDddKernelTests).Assembly);

        await using var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredService<ISender>();
        var response = await sender.Send(new PingCommand("pong"));

        Assert.Equal("pong", response);
    }

    private sealed class EmailAddress(string value) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return value;
        }
    }

    private sealed class CustomerCode(string value) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return value;
        }
    }

    private sealed class Customer(Guid id) : Entity<Guid>(id);

    private sealed class OtherCustomer(Guid id) : Entity<Guid>(id);

    private sealed class CustomerAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Registered()
        {
            AddDomainEvent(new CustomerRegistered(Id));
        }
    }

    public sealed class CustomerRegistered : DomainEvent
    {
        public CustomerRegistered(Guid customerId)
        {
            CustomerId = customerId;
        }

        public Guid CustomerId { get; }
    }
}

public sealed class HandledEventState
{
    public int Count { get; set; }
}

public sealed class CustomerRegisteredHandler(HandledEventState state)
    : IDomainEventHandler<FrameworkDddKernelTests.CustomerRegistered>
{
    public Task HandleAsync(
        FrameworkDddKernelTests.CustomerRegistered domainEvent,
        CancellationToken cancellationToken = default)
    {
        state.Count++;
        return Task.CompletedTask;
    }
}

public sealed record PingCommand(string Value) : ICommand<string>;

public sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    public Task<string> HandleAsync(
        PingCommand command,
        CancellationToken cancellationToken = default)
        => Task.FromResult(command.Value);
}
