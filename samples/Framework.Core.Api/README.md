# Framework.Core.Api

This is a real external-consumer simulation for Aspros Basic Framework 2.0.0 / .NET 10.

The API does not reference the Framework projects directly. It references `Aspros.Base.Framework.Infrastructure` version `2.0.0`.

The regression script first packs the Framework projects into a disposable local NuGet feed, restores this API from that feed, builds it, starts the service on `http://127.0.0.1:7210`, and verifies `GET /health`.

Run:

```bash
bash dev-environment/run-core-package-consumer-regression.sh
```

This models the way a real business service consumes a released Framework package.