# Application Layer Design

## Goal

The Application Layer orchestrates business use cases without containing core domain rules.

## Layer Relationship

```text
API / MQ / Job / RPC
     |
     v
Command / Query
     |
     v
Application Handler
     |
     +--> Domain Event Handler (when needed)
     |
     +--> Integration Event Publisher (when needed)
     |
     v
Domain
     |
     v
Infrastructure implementations are accessed through Application abstractions
```

The dependency direction remains:

```text
Infrastructure → Application → Domain
```

Application must not directly depend on concrete databases, message brokers, HTTP clients, or other infrastructure implementations.

## Command

A Command represents a business operation that changes system state.

## Query

A Query reads data without changing domain state.

## Handler

A Handler coordinates domain objects, persistence abstractions, and external service abstractions.

## Domain Events

Application provides the minimal domain-event handling boundary:

- `IDomainEventHandler<TDomainEvent>`
- `IDomainEventDispatcher`

Domain Event Handlers process events raised by aggregates.

They may:

- update other state that must be maintained synchronously;
- call Application-level abstractions;
- publish Integration Events;
- coordinate other use-case capabilities.

They should not introduce concrete CAP, RabbitMQ, or similar infrastructure dependencies into the Domain layer.

## Integration Events

Application uses `IIntegrationEventPublisher` to express the need to publish cross-service messages.

Concrete implementations belong to Infrastructure, such as the current CAP implementation.

```text
Domain Event
      ↓
Application Handler
      ↓
IIntegrationEventPublisher
      ↓
Infrastructure / CAP
```

Domain Events and Integration Events remain separate concepts. Not every Domain Event is automatically sent to a message broker.

## Current Strategy

Framework v10 currently provides only the minimum Application foundation:

- Command / Query abstractions
- Handler abstractions
- Optional Pipeline mechanism
- Domain Event Handler / Dispatcher
- Integration Event Publisher

Validation, Idempotency, and additional Pipeline Behaviors should be added only when stable real-world requirements emerge.

The framework does not introduce a complete Application Runtime, validation framework, or message abstraction merely for architectural completeness.

## RPC Boundary

Framework v10 keeps RPC support but defines only the minimal calling contract in Application:

`IRpcClient`

Application expresses remote-service calls through this contract without depending directly on gRPC, Dubbo, or another concrete RPC framework.

Concrete implementations live in Infrastructure:

```text
Application
    |
    | IRpcClient
    v
Infrastructure
    |
    +-- gRPC
    +-- Dubbo
    +-- other RPC implementations
```

The framework intentionally does not define:

- RPC registry abstraction
- service discovery abstraction
- load-balancing abstraction
- RPC retry/circuit-breaker framework
- serialization protocol abstraction

These capabilities should be reused from the actual RPC technology in use. Common abstractions should be promoted into the framework only after multiple projects develop a stable repeated need.

RPC does not change the core Application responsibility: the Handler orchestrates the use case, while RPC is only one possible infrastructure dependency.
