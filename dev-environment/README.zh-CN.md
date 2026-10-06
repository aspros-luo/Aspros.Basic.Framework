# Framework 临时开发 / 测试环境

提供可重复、可销毁的 Docker 验证环境：.NET 8 SDK、EF Core CLI 8.0.12、MySQL 8.4。

## 启动

```bash
cd dev-environment
docker compose up -d mysql
docker compose --profile test run --rm framework-test
```

进入测试容器：

```bash
docker compose run --rm framework-test bash
dotnet build Aspros.Base.Framework.sln
dotnet ef --version
```

销毁并删除测试数据库：

```bash
docker compose down -v
```

这不是生产部署配置，也不会创建 GitHub Actions。后续 Migration Matrix 将以此作为可重复实验环境。
