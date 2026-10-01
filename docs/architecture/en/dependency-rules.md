# Framework v10 Dependency Rules

## Layer Rules

### Domain

Domain must not depend on:

- ORM
- Database providers
- Message brokers
- External infrastructure frameworks

Domain events are domain concepts only. Dispatching and publishing are owned by higher layers.

### Application

Application coordinates business use cases and depends on Domain abstractions.

Application defines contracts such as:

- Domain Event Handler / Dispatcher
- Integration Event Publisher
- Unit of Work
- RPC client abstraction

These contracts must remain independent from concrete infrastructure technologies.

### Infrastructure

Infrastructure provides implementations for external technologies, including:

- EF Core
- Dapper
- ClickHouse
- CAP
- RPC implementations

## Dependency Direction

```text
Infrastructure → Application → Domain
```

Dependencies should always point toward business concepts.

## Domain Event Boundary

```text
Domain
  |
  | IDomainEvent
  v
Application
  |
  | IDomainEventHandler / IIntegrationEventPublisher
  v
Infrastructure
  |
  +--> EF Core / CAP / Message Broker
```

Concrete message-broker or Outbox technology must not leak into Domain.
