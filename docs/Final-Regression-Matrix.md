# Framework Regression Matrix — Paused

This document is retained as a historical record. The Framework xUnit and migration regression execution path is intentionally paused and is no longer the active Docker deployment or release-acceptance workflow.

## Active workflow

Deploy the local infrastructure and sample APIs:

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api up -d --build
```

Call the HTTP API and its downstream gRPC provider:

```bash
curl http://localhost:7141/health
curl "http://localhost:7141/core/ping?message=hello"
curl http://localhost:17041/health
```

See dev-environment/README.md and dev-environment/README.zh-CN.md for endpoints, credentials and lifecycle commands.

## Historical Framework test result

The last reported xUnit execution ran 44 cases: 38 passed, 4 skipped and 2 failed. Two test-harness corrections were committed afterward, but no subsequent run confirmed them. This historical result is not a PASS and is not a reason to block the current Docker environment deployment.

The test source remains under tests/ for possible future reactivation. Do not start the old framework-test, provider-test, migration-test or full-regression Compose runners; those services have been removed from the active Compose configuration.

## Verification boundaries

- Docker container health confirms that a container process is running and its configured health endpoint responds.
- API smoke calls confirm only the behavior actually exercised by those calls. The current sample covers Consumer HTTP -> Provider gRPC.
- Starting MySQL, Redis, RabbitMQ and Nacos does not by itself verify Framework integrations with those services.
- Building the API images compiles the API projects and their project references, but does not execute the Framework test project.
