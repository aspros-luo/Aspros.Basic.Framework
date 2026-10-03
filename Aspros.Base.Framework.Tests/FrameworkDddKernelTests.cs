using ApplicationWorkContext = Aspros.Base.Framework.Application.Abstractions.IWorkContext;
using Aspros.Base.Framework.Application.Abstractions;
using Aspros.Base.Framework.Application.Abstractions.Events;
using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Aspros.Base.Framework.Domain.Kernel;
using Aspros.Base.Framework.Infrastructure;
using Aspros.Base.Framework.Infrastructure.Event;
using Aspros.Base.Framework.Infrastructure.Persistence;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
        Assert.False(first.Equals(differentType));
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
    public void Kernel_aggregate_remains_compatible_with_legacy_aggregate_contract()
    {
        var aggregate = new CustomerAggregate(Guid.NewGuid());

        Assert.IsAssignableFrom<Aspros.Base.Framework.Domain.IAggregateRoot>(aggregate);

        AcceptsLegacyRepositoryContract<CustomerAggregate>();
    }

    private static void AcceptsLegacyRepositoryContract<TAggregate>()
        where TAggregate : class, Aspros.Base.Framework.Domain.IAggregateRoot
    {
        _ = typeof(IRepositoryMarker<TAggregate>);
    }

    [Fact]
    public async Task EfUnitOfWork_commit_only_persists_without_dispatching_events()
    {
        var dbContext = new CountingDbContext();
        var dispatcher = new CountingDomainEventDispatcher();
        var unitOfWork = new EfUnitOfWork(dbContext, new NoopWorkContext(), dispatcher);

        var result = await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(1, dbContext.SaveChangesCalls);
        Assert.Equal(0, dispatcher.DispatchCalls);
    }


    [Fact]
    public async Task Explicit_transaction_dispatches_events_and_persists_handler_changes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<TransactionalTestDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new TransactionalTestDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<
            IDomainEventHandler<TransactionTestAggregateCreated>,
            TransactionTestAggregateCreatedHandler>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        var unitOfWork = new EfUnitOfWork(dbContext, new NoopWorkContext(), dispatcher);

        var aggregate = new TransactionTestAggregate(Guid.NewGuid());
        aggregate.RaiseCreatedEvent();

        await unitOfWork.RegisterNew(aggregate);
        await unitOfWork.ExecuteInTransactionAsync(
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, dbContext.SaveChangesCalls);
        Assert.Single(
            await dbContext.ProcessedEvents.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(aggregate.GetDomainEvents());
    }

    [Fact]
    public async Task Domain_event_handler_failure_rolls_back_the_explicit_transaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<TransactionalTestDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new TransactionalTestDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<
            IDomainEventHandler<TransactionTestAggregateCreated>,
            ThrowingTransactionEventHandler>();

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();
        var unitOfWork = new EfUnitOfWork(dbContext, new NoopWorkContext(), dispatcher);

        var aggregate = new TransactionTestAggregate(Guid.NewGuid());
        aggregate.RaiseCreatedEvent();

        await unitOfWork.RegisterNew(aggregate);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                _ => Task.CompletedTask,
                TestContext.Current.CancellationToken));

        await using var verificationContext = new TransactionalTestDbContext(options);

        Assert.Empty(
            await verificationContext.Aggregates.ToListAsync(
                TestContext.Current.CancellationToken));
        Assert.Empty(
            await verificationContext.ProcessedEvents.ToListAsync(
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Explicit_registration_works_without_AutoInject()
    {
        var services = new ServiceCollection();
        services.AddSingleton<HandledEventState>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<FrameworkDddKernelTests.CustomerRegistered>, CustomerRegisteredHandler>();
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(FrameworkDddKernelTests).Assembly));

        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync(
            [new CustomerRegistered(Guid.NewGuid())],
            TestContext.Current.CancellationToken);

        var state = provider.GetRequiredService<HandledEventState>();
        Assert.Equal(1, state.Count);

        var sender = provider.GetRequiredService<ISender>();
        var response = await sender.Send(
            new PingCommand("pong"),
            TestContext.Current.CancellationToken);

        Assert.Equal("pong", response);
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
        await dispatcher.DispatchAsync(
            [new CustomerRegistered(Guid.NewGuid())],
            TestContext.Current.CancellationToken);

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
        var response = await sender.Send(
            new PingCommand("pong"),
            TestContext.Current.CancellationToken);

        Assert.Equal("pong", response);
    }

    private interface IRepositoryMarker<TAggregate>
        where TAggregate : class, Aspros.Base.Framework.Domain.IAggregateRoot
    {
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


public sealed class TransactionTestAggregate(Guid id)
    : AggregateRoot<Guid>(id)
{
    public void RaiseCreatedEvent()
    {
        AddDomainEvent(new TransactionTestAggregateCreated(Id));
    }

    public IReadOnlyCollection<IDomainEvent> GetDomainEvents()
        => ((IAggregateRoot)this).DomainEvents;
}

public sealed class TransactionTestAggregateCreated(Guid aggregateId) : DomainEvent
{
    public Guid AggregateId { get; } = aggregateId;
}

public sealed class ProcessedDomainEvent
{
    public int Id { get; set; }
    public Guid AggregateId { get; set; }
}

public sealed class TransactionalTestDbContext(
    DbContextOptions<TransactionalTestDbContext> options)
    : DbContext(options), IDbContext
{
    public DbSet<TransactionTestAggregate> Aggregates => Set<TransactionTestAggregate>();

    public DbSet<ProcessedDomainEvent> ProcessedEvents => Set<ProcessedDomainEvent>();

    public int SaveChangesCalls { get; private set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransactionTestAggregate>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<ProcessedDomainEvent>()
            .HasKey(x => x.Id);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return await base.SaveChangesAsync(cancellationToken);
    }
}

public sealed class TransactionTestAggregateCreatedHandler(
    TransactionalTestDbContext dbContext)
    : IDomainEventHandler<TransactionTestAggregateCreated>
{
    public Task HandleAsync(
        TransactionTestAggregateCreated domainEvent,
        CancellationToken cancellationToken = default)
    {
        dbContext.ProcessedEvents.Add(new ProcessedDomainEvent
        {
            AggregateId = domainEvent.AggregateId
        });

        return Task.CompletedTask;
    }
}

public sealed class ThrowingTransactionEventHandler
    : IDomainEventHandler<TransactionTestAggregateCreated>
{
    public Task HandleAsync(
        TransactionTestAggregateCreated domainEvent,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("transaction event handler failed");
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

public sealed class CountingDbContext : DbContext, IDbContext
{
    public int SaveChangesCalls { get; private set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}

public sealed class NoopWorkContext : ApplicationWorkContext
{
    public Task<long> GetUserId() => throw new NotSupportedException();
    public Task<long> GetTenantId() => throw new NotSupportedException();
    public Task<T> Get<T>(string key) => throw new NotSupportedException();
}

public sealed class CountingDomainEventDispatcher : IDomainEventDispatcher
{
    public int DispatchCalls { get; private set; }

    public Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        DispatchCalls++;
        return Task.CompletedTask;
    }
}
