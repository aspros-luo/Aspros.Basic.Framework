# Framework Regression Tests — Paused

The Framework xUnit and migration test projects are retained as source, but their Docker execution path is intentionally paused.

Do not use the former `framework-test`, `framework-provider-test`, `framework-migration-test` or `framework-full-regression` commands as the current environment startup or acceptance workflow.

The active workflow is infrastructure deployment plus API-level verification. See:
- `dev-environment/README.md`
- `dev-environment/README.zh-CN.md`

The test source has not been deleted. It can be reviewed or re-enabled separately after the API runtime workflow is established. Container startup and API responses must be reported separately from Framework unit/integration-test status.
