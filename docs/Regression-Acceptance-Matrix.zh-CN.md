# 旧版回归验收矩阵（Legacy）

> Framework 已升级到 .NET 10 / 2.0.0。新的唯一验收入口是 `docs/Final-Regression-Matrix.zh-CN.md`。
>
> 本文件保留历史记录用途，不再作为当前版本的验收基准。

请使用：

`cd dev-environment && docker compose --profile infra --profile full-regression run --rm framework-full-regression`

当前 Runtime 验证状态仍以最终矩阵为准；本环境未安装 .NET SDK / Docker，因此不能把静态检查冒充为真实运行结果。
