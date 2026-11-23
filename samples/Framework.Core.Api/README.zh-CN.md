# Framework.Core.Api

这是一个独立的外部消费者示例，用于展示 .NET 10 业务 API 如何通过 NuGet `PackageReference` 引用 `Aspros.Base.Framework.Infrastructure`，而不是通过 `ProjectReference` 直接引用 Framework 源码。

此前的临时 NuGet 打包消费者回归脚本已经退役，不再属于当前 Docker 环境工作流。

该示例保留为手动包消费参考。当前 Docker API 环境使用 `Framework.Core.Provider` 与 `Framework.Core.Consumer`，启动方式见 `dev-environment/README.zh-CN.md`。
