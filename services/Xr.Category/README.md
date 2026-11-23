# Xr.Category

类目业务服务。此目录属于 `services/`，与 Framework 的 `samples/` 示例项目分开维护。

## Project structure

```text
Xr.Category/
├── Xr.Category.sln
├── Directory.Build.props
└── src/
    ├── Xr.Category.Api/
    ├── Xr.Category.Application/
    ├── Xr.Category.Domain/
    └── Xr.Category.Infrastructure/
```

依赖职责：

- **Api**：HTTP 入口、配置和服务组合。
- **Application**：业务用例与应用层协调。
- **Domain**：类目领域模型、领域规则和仓储查询契约。
- **Infrastructure**：MySQL、DbContext、EF Mapping 和仓储实现。

当前 Framework 把 `IUnitOfWork` 公共契约放在 `Aspros.Base.Framework.Infrastructure` 程序集中，因此 Application 需要引用该程序集来使用 UoW；Application 不直接依赖 EF Core Provider，也不负责数据库映射。

## Build and run

从仓库根目录执行：

```bash
dotnet build services/Xr.Category/Xr.Category.sln

dotnet run --project services/Xr.Category/src/Xr.Category.Api/Xr.Category.Api.csproj -- --urls http://localhost:7242
```

检查 API 存活状态：

```bash
curl --fail http://localhost:7242/health/live
```

检查 MySQL 是否可连接：

```bash
curl --fail http://localhost:7242/health/ready
```

`/health/ready` 只验证数据库连接，不会创建数据库、表或执行 Migration。

## Local MySQL

默认连接字符串使用当前 Compose MySQL：

- Host: `localhost:13306`
- Database: `aspros_category`
- User: `framework`

当前 Compose 环境会创建 `aspros_framework_test`，不会自动创建类目服务数据库。使用 MySQL root 账号手动执行：

```sql
CREATE DATABASE aspros_category
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

GRANT ALL PRIVILEGES ON aspros_category.* TO 'framework'@'%';
```

也可以通过 `ConnectionStrings__Category` 环境变量覆盖连接字符串。容器内连接 MySQL 时，Host 应使用 Compose 服务名 `mysql`，而不是 `localhost`。

数据库结构仍由业务开发者显式管理。服务启动时不会调用 `EnsureCreated` 或 `Database.Migrate`。

## Framework reference mode

由于服务目前与 Framework 位于同一仓库，项目先通过 `ProjectReference` 引用 Framework 源码，便于在 RC 阶段同步开发和调试。待 Framework 包发布并确定服务独立仓库/版本策略后，再切换为 NuGet `PackageReference`。

## Domain scope

当前提交只建立服务边界、依赖注册和数据库连接检查。类目实体字段、单级/多级结构、排序、停用、删除和父子关系规则尚未预设，确定这些业务规则后再进入 Domain 与 CRUD 实现。
