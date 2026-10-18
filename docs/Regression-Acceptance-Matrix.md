# Legacy Regression Acceptance Matrix

> The framework is now on .NET 10 / 2.0.0. The single current acceptance source is `docs/Final-Regression-Matrix.md`.
>
> This file is retained for historical reference and is no longer the acceptance baseline.

Use:

`cd dev-environment && docker compose --profile infra --profile full-regression run --rm framework-full-regression`

Runtime status is tracked in the final matrix. This execution environment has no .NET SDK or Docker, so static inspection must not be reported as runtime PASS.
