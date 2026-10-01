# Framework v10 Dependency Rules

## Layer Rules

### Domain

Domain must not depend on:

- ORM
- Database providers
- Message brokers
- External infrastructure frameworks

### Application

Application coordinates business use cases and depends on domain abstractions.

### Infrastructure

Infrastructure provides implementations for external technologies.

## Dependency Direction

Infrastructure -> Application -> Domain

Dependencies should always point toward business concepts.
