# Framework Consumer API Validation

This sample simulates a small ASP.NET Core Web API consuming the framework's Application, Domain, and Infrastructure layers.

## Run the API smoke tests

From the repository root, with the .NET 8 SDK installed:

```bash
bash validation/scripts/smoke-test.sh
```

The script starts the API on `127.0.0.1:5087`, uses a temporary working directory for the SQLite database, sends HTTP requests, checks response status codes and JSON payloads, and shuts the API down afterward. Set `PORT` to use another port. Set `KEEP_TEST_ARTIFACTS=1` to preserve the temporary API log and database for troubleshooting.

## Scenarios covered

| Scenario | Expected result |
| --- | --- |
| Create an order with `productName=Coffee` | `201 Created` with an order ID; no Domain Event is raised |
| Retrieve the created order | `200 OK`, correct product name, `confirmed=false`, no audit |
| Confirm the created order | `200 OK`; explicit transaction path is used |
| Retrieve the confirmed order | `200 OK`, `confirmed=true`, audit contains `Order confirmed: Coffee` |
| Retrieve an unknown order ID | `404 Not Found` |
| Omit `productName`, send an empty/whitespace value, or send JSON `null` | `400 Bad Request` |
| Inspect the integration-event publisher | Contains `validation.order.confirmed` |

## What this validates

- MediatR resolves the Command and Query handlers registered through `AutoInject()`.
- The consumer can use `ICommand<TResult>`, `IQuery<TResult>`, and their handler contracts.
- Simple order creation uses `IUnitOfWork.CommitAsync()` without an explicit application transaction or Domain Event.
- Order confirmation explicitly uses `IUnitOfWork.ExecuteInTransactionAsync()`.
- The confirmation Domain Event is handled inside that transaction and writes an audit record.
- The domain-event handler depends on `IIntegrationEventPublisher`, not a broker-specific API.
- The API distinguishes missing resources and invalid create requests.

## Important limits

The sample uses SQLite and a test-only in-memory `IIntegrationEventPublisher`. It validates the application integration boundary, not actual broker delivery, CAP Outbox durability, cross-service retries, or production database behavior.

The smoke-test script has been added to the repository, but it must be run in an environment with the .NET 8 SDK and package restore access before its HTTP assertions can be reported as executed. This branch does not require or trigger GitHub Actions.
