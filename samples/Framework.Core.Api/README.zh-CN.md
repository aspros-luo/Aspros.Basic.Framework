# Framework.Core.Api

这是一个独立的外部消费者示例，用于验证 .NET 10 业务 API 通过 NuGet `PackageReference` 消费全部五个 Framework 包：Abstractions、Domain、Infrastructure、Algorithms 和 ClickHouse。它不通过 `ProjectReference` 直接引用 Framework 源码。

在仓库根目录执行独立包消费验收：

```bash
bash scripts/verify-packaged-consumer.sh
```

当前 Docker API 环境使用 `Framework.Core.Provider` 与 `Framework.Core.Consumer`，启动方式见 `dev-environment/README.zh-CN.md`。
