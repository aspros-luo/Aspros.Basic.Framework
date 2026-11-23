# Framework Core Provider / Consumer

该示例演示标准的服务间调用链：

`Framework.Core.Consumer` 对外提供 HTTP API，并通过生成的 gRPC Client 调用 `Framework.Core.Provider`。

## 本地运行

先启动 Provider：

```bash
dotnet run --project samples/Framework.Core.Provider
```

再在另一个终端启动 Consumer：

```bash
dotnet run --project samples/Framework.Core.Consumer
```

Provider 的 HTTP 健康检查地址为 `http://localhost:8080/health`，gRPC/HTTP2 地址为 `http://localhost:7041`。Consumer 的 HTTP 地址为 `http://localhost:7141`。

调用 Consumer：

```bash
curl "http://localhost:7141/core/ping?message=hello"
```

## Docker

完整启动方式见 `dev-environment/README.zh-CN.md`（英文见 `dev-environment/README.md`）。
