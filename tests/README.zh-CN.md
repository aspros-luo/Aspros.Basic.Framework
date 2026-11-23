# Framework 回归测试（暂时停用）

Framework 的 xUnit 和 Migration 测试项目源码暂时保留，但已暂停通过 Docker 执行这些测试。

当前不要再使用旧的 `framework-test`、`framework-provider-test`、`framework-migration-test` 或 `framework-full-regression` 命令作为环境启动或验收流程。

当前工作流改为先部署基础设施，再通过真实 API 进行验证。请以以下文档为准：
- `dev-environment/README.zh-CN.md`
- `dev-environment/README.md`

测试源码没有删除。待 API 运行环境稳定后，可以单独评估是否重新启用。必须区分 Docker 容器启动状态、API 请求结果和 Framework 内部测试结果，不能互相替代。
