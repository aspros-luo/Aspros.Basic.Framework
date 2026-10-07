# Framework Regression Tests

The preferred disposable acceptance entry point is `docker compose --profile infra --profile full-regression run --rm framework-full-regression`.

This is the independent regression test project.

The final matrix covers DI, AutoInject, UnitOfWork, Repository, EF mapping, migrations, gRPC, service discovery, permission, HTTP resilience, health, rate limiting, CAP, compatibility and serialization.

A disposable MySQL migration fixture now exercises add, idempotent script generation, protected update, repeated update, a reviewed rename migration and data-preservation verification.

HTTP regression tests execute transient retry, unsafe-method retry protection, liveness/readiness endpoints, rate-limit rejection, and permission 403/503 scenarios.

The provider profile also executes a real Redis distributed-cache round trip.

Provider- and infrastructure-dependent scenarios must run inside the disposable Docker environment rather than being represented by mocks.

A module is PASS only after real build/test execution. Static inspection must never be reported as runtime PASS.
