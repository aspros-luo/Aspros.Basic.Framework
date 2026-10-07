# Framework.Core.Api

这是针对 Aspros Basic Framework 2.0.0 / .NET 10 的真实外部消费者模拟。

这个 Web API 不使用 ProjectReference 直接引用 Framework 源码，而是通过 NuGet PackageReference 引用 `Aspros.Base.Framework.Infrastructure` 版本 `2.0.0`。

回归脚本会先把 Framework 打包到一次性的本地 NuGet 源，然后让 Core API 从该 NuGet 源恢复依赖、编译并启动服务，最后请求 `GET /health`。

默认地址：`http://127.0.0.1:7210`

执行：

```bash
bash dev-environment/run-core-package-consumer-regression.sh
```

这个过程用于模拟真实业务服务发布后通过 Framework NuGet 包进行消费。