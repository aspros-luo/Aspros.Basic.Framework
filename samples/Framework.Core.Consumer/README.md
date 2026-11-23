# Framework Core Provider / Consumer

This sample demonstrates a service-to-service call:

`Framework.Core.Consumer` exposes an HTTP API and invokes `Framework.Core.Provider` through the generated gRPC client.

## Local run

Start the Provider:

```bash
dotnet run --project samples/Framework.Core.Provider
```

Start the Consumer in another terminal:

```bash
dotnet run --project samples/Framework.Core.Consumer
```

The Provider exposes its HTTP health endpoint at `http://localhost:8080/health` and its gRPC/HTTP2 endpoint at `http://localhost:7041`. The Consumer exposes HTTP at `http://localhost:7141`.

Call the Consumer:

```bash
curl "http://localhost:7141/core/ping?message=hello"
```

## Docker

Use the complete stack described in `dev-environment/README.md` or `dev-environment/README.zh-CN.md`.
