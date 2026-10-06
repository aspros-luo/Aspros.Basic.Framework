# Framework Regression Tests

This is the independent regression test project.

The final matrix covers DI, AutoInject, UnitOfWork, Repository, EF mapping, migrations, gRPC, service discovery, permission, HTTP resilience, health, rate limiting, CAP, compatibility and serialization.

Provider- and infrastructure-dependent scenarios must run inside the disposable Docker environment rather than being represented by mocks.

A module is PASS only after real build/test execution. Static inspection must never be reported as runtime PASS.
