# Xr.Category

类目服务目前作为 Aspros Framework 的真实消费方和集成验证服务使用。当前目标是验证框架 API 在业务代码中的实际使用体验，不代表类目产品的最终领域设计。

## Project structure

```text
Xr.Category/
├── Xr.Category.sln
├── Directory.Build.props
├── database/
│   └── 001_create_category.sql
└── src/
    ├── Xr.Category.Api/
    ├── Xr.Category.Application/
    ├── Xr.Category.Domain/
    └── Xr.Category.Infrastructure/
```

依赖职责：

- **Api**：HTTP 入口、配置和服务组合。
- **Application**：业务用例、请求/响应 DTO，以及 Dapper 查询契约。
- **Domain**：用于验证聚合根、基础实体、仓储契约和关系映射的最小 Category 模型。
- **Infrastructure**：MySQL、DbContext、EF Mapping、Repository，以及 Dapper SQL 实现。

当前 Framework 把 `IUnitOfWork` 和 `IDapperExecutor` 公共契约放在 `Aspros.Base.Framework.Infrastructure` 程序集中，因此 Application 需要引用该程序集来使用这些契约；EF Provider、DbContext 和 SQL 实现仍由 Infrastructure 负责。

## Build and run

从仓库根目录执行：

```bash
dotnet build services/Xr.Category/Xr.Category.sln

ASPNETCORE_ENVIRONMENT=Development dotnet run --project services/Xr.Category/src/Xr.Category.Api/Xr.Category.Api.csproj -- --urls http://localhost:7242
```

检查 API 存活状态和数据库连接：

```bash
curl --fail http://localhost:7242/health/live
curl --fail http://localhost:7242/health/ready
```

`/health/ready` 会检查 MySQL 和 ClickHouse 是否可连接，不会创建数据库、表或执行 Migration。

## Local MySQL

默认连接字符串使用当前 Compose MySQL：

- Host: `localhost:13306`
- Database: `aspros_category`
- User: `framework`

当前 Compose 环境只会创建 `aspros_framework_test`。类目服务数据库由开发者显式创建，并按前序步骤给 `framework` 用户授权。

数据库表结构由开发者手动执行仓库中的 SQL 文件，不由 API 在启动时自动创建：

```bash
docker compose -f dev-environment/docker-compose.yml exec -T mysql \
  mysql -uroot -proot_password aspros_category \
  < services/Xr.Category/database/001_create_category.sql
```

也可以通过 `ConnectionStrings__Category` 环境变量覆盖连接字符串。容器内连接 MySQL 时，Host 应使用 Compose 服务名 `mysql`，而不是 `localhost`。

## Redis Cache validation

The API registers the standard `IDistributedCache` Redis provider using `ConnectionStrings:Redis`. The Category detail read (`GET /api/categories/{id}`) uses a small Cache-Aside flow:

- Read from Redis first; on a miss, load the active category from MySQL and cache the DTO.
- Category detail entries use a five-minute absolute TTL.
- Successful updates and logical deletes invalidate the corresponding detail key.
- If Redis is unavailable, reads fall back to MySQL and cache write/invalidation failures are logged. Cache availability is not required for the underlying CRUD operation.

The configured Redis key prefix is `xr-category:`; the detail key for category ID 1 is `xr-category:category:detail:1`.

Inspect the key and its remaining TTL:

```bash
docker compose -f dev-environment/docker-compose.yml exec redis \
  redis-cli --scan --pattern 'xr-category:category:detail:*'

docker compose -f dev-environment/docker-compose.yml exec redis \
  redis-cli TTL xr-category:category:detail:1
```

To validate cache-aside behavior, request the same category twice, inspect the key, update that category, and inspect the key again. The first read populates Redis; the second read can be served from Redis; a successful update removes the cached detail so the next read reloads the new value from MySQL.

## EF Core CRUD validation

以下 API 验证 Framework 的 EF Core Mapping 自动发现、Repository 查询、UoW 新增/更新/逻辑删除及 Commit。

### 1. Create a parent category

```bash
curl -i -X POST http://localhost:7242/api/categories \
  -H 'Content-Type: application/json' \
  -d '{
    "code": "ELECTRONICS",
    "name": "Electronics",
    "sortOrder": 10,
    "isEnabled": true
  }'
```

响应包含数据库生成的 `id`。下面的子类目请求使用该 ID 作为 `parentId`。

### 2. Create a child category

```bash
curl -i -X POST http://localhost:7242/api/categories \
  -H 'Content-Type: application/json' \
  -d '{
    "code": "PHONES",
    "name": "Phones",
    "parentId": 1,
    "sortOrder": 20,
    "isEnabled": true
  }'
```

### 3. Read and list

```bash
curl --fail http://localhost:7242/api/categories/1
curl --fail 'http://localhost:7242/api/categories?page=1&pageSize=20'
```

列表查询通过 Repository/EF Core 执行，按排序字段稳定排序并排除逻辑删除记录。

### 4. Update

```bash
curl -i -X PUT http://localhost:7242/api/categories/1 \
  -H 'Content-Type: application/json' \
  -d '{
    "code": "ELECTRONICS",
    "name": "Consumer Electronics",
    "sortOrder": 10,
    "isEnabled": true
  }'
```

更新路径先通过 Repository 加载跟踪实体，再调用 `RegisterDirty` 和 `CommitAsync`。这也用于观察 UoW 是否保留 EF Core 的属性级变更跟踪。

### 5. Logical delete

```bash
curl -i -X DELETE http://localhost:7242/api/categories/2
curl -i http://localhost:7242/api/categories/2
```

删除调用 `RegisterDeleted` 的默认逻辑删除模式。记录保留在数据库中，但 Repository 查询不再返回它。

## Dapper complex query validation

`GET /api/categories/search/dapper` 走 `IDapperExecutor.QueryAsync<T>` 和 `QuerySingleOrDefaultAsync<T>`，不经过 EF Core LINQ。

查询包含：

- 可选关键字过滤（名称或编码）；
- 可选父类目、启用状态过滤；
- Self JOIN 读取父类目名称；
- LEFT JOIN + COUNT 聚合子类目数量；
- 分页、稳定排序；
- 独立 Count 查询返回总数；
- 所有查询值使用 Dapper 参数绑定，不拼接用户输入。

示例：

```bash
curl --fail \
  'http://localhost:7242/api/categories/search/dapper?keyword=phone&parentId=1&isEnabled=true&page=1&pageSize=20'
```

查询父类目并查看子类目数量：

```bash
curl --fail \
  'http://localhost:7242/api/categories/search/dapper?keyword=ELECTRONICS&page=1&pageSize=20'
```

该查询用于验证 Dapper 对多条件、关联和聚合读取的使用体验。此服务没有为 Dapper 另造一层通用查询框架。

## Explicit UnitOfWork transaction validation

The Development-only diagnostic endpoint validates explicit transaction commit and rollback against MySQL:

```bash
curl -i -X POST http://localhost:7242/_dev/validation/unit-of-work
```

The endpoint creates two rows in one UnitOfWork transaction and commits them, then creates two more rows in a separate transaction. For the rollback case it calls `DbContext.SaveChangesAsync` while the transaction is still open before invoking `RollbackAsync`; this ensures SQL has actually reached MySQL and the rollback is not merely clearing unpersisted ChangeTracker entries.

Both cases are verified through a fresh dependency-injection scope and DbContext. A successful response reports two committed rows and zero rows remaining from the rolled-back transaction. The endpoint is not mapped outside the Development environment and is intended only for local framework validation, not as a production business API.

## RabbitMQ / CAP validation

The API uses `AddFrameworkCap<CategoryDbContext>()`: CAP stores its local published/received message records in the Category MySQL database and uses RabbitMQ as the transport. CAP's MySQL provider initializes its own `cap`-prefixed infrastructure tables at startup; it does not create or migrate the Category business schema. The EF Core provider is `Microting.EntityFrameworkCore.MySql` (10.0.14), built on `MySqlConnector`, matching the driver used by `DotNetCore.CAP.MySql`. Keep both providers on the same ADO.NET driver because CAP shares the EF Core connection and transaction when writing the outbox.

Before running the validation, create the separate business receipt table manually:

```bash
docker compose -f dev-environment/docker-compose.yml exec -T mysql \
  sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" aspros_category' \
  < services/Xr.Category/database/002_create_cap_validation_receipt.sql
```

The receipt entity uses ordinary .NET strings with maximum lengths; the MySQL columns are `VARCHAR(36)` and `VARCHAR(32)`. The Category EF Core provider (`Microting.EntityFrameworkCore.MySql`) and CAP's MySQL storage provider both use `MySqlConnector`, so they can share the same connection and transaction without mixing incompatible ADO.NET parameter types.

If you already created the table from an earlier version of this script, `CREATE TABLE IF NOT EXISTS` will not change its existing column definitions. Upgrade it once manually:

```sql
ALTER TABLE category_cap_validation_receipt
    MODIFY COLUMN event_id VARCHAR(36) NOT NULL,
    MODIFY COLUMN run_id VARCHAR(32) NOT NULL;
```

The Development-only validation endpoints exercise four reliability properties:

1. **Transactional outbox commit**: a Category row and a CAP message are written in one CAP-aware EF Core transaction.
2. **Transactional rollback**: a second Category row and its pending CAP message are written, then the same transaction is rolled back. Neither should become visible to the consumer.
3. **Retry**: the consumer intentionally fails the first delivery of one event. CAP should redeliver it; the receipt should be written after the retry succeeds.
4. **Idempotency**: two separate CAP messages carry the same business `EventId`. The consumer uses a MySQL primary key plus `INSERT IGNORE`, so both deliveries should create only one business receipt.

Start the API in Development mode (RabbitMQ must be running):

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project services/Xr.Category/src/Xr.Category.Api/Xr.Category.Api.csproj -- --urls http://localhost:7242
```

Start a validation run and retain the returned `runId`:

```bash
curl -i -X POST http://localhost:7242/_dev/validation/cap
```

Poll the result using that `runId`:

```bash
curl --fail http://localhost:7242/_dev/validation/cap/<runId>
```

The final response should report one committed Category row, zero rolled-back Category rows, at least two deliveries for the retry event with one receipt, at least two deliveries for the duplicate event with one receipt, and zero deliveries/receipts for the rolled-back message.

The attempt counter is intentionally in memory because it is diagnostic evidence for this local test only. The business deduplication record is stored in MySQL so the idempotency key and business effect share the database's atomicity boundary. CAP delivery is at-least-once; consumers must remain idempotent. Do not interpret this local validation as an exactly-once delivery guarantee.

## ClickHouse analytics validation

ClickHouse is used as an analytical store for high-volume behavior events and aggregate reads. It is deliberately separate from the Category MySQL DbContext, EF Core and Dapper paths; it is not a replacement for transactional business storage.

The service uses the official `ClickHouse.Driver` package and its thread-safe `ClickHouseClient` as a singleton. The local Compose service exposes HTTP on `localhost:18123` and native TCP on `localhost:19000`. The Category API uses the HTTP endpoint.

Start the infrastructure profile:

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra up -d clickhouse
```

Create the analytics database and event table explicitly:

```bash
docker compose -f dev-environment/docker-compose.yml exec -T clickhouse \
  clickhouse-client --user framework --password framework_password \
  < services/Xr.Category/database/003_create_category_behavior_events.sql
```

The schema is a monthly-partitioned `MergeTree` ordered by user, event time and event ID. Table creation is a manual deployment step; the API does not create or migrate ClickHouse tables.

With the Category API running in Development mode, execute the one-event write/read smoke test:

```bash
curl -i -X POST http://localhost:7242/_dev/validation/clickhouse
```

A successful response reports `passed: true`, `insertedRows: 1` and `observedRows: 1`, together with the ClickHouse server version and generated run/event IDs. This validates connectivity, binary insert and query through the official client. It does not yet validate CAP delivery or deduplication; those belong to the next behavior-event integration step.

### CAP asynchronous behavior-event ingestion

The Development-only CAP validation publishes one retry event and two independent messages with the same business `EventId`. The subscriber intentionally fails the first delivery of the retry event, then writes successful deliveries to ClickHouse through the official client.

Start the validation:

```bash
curl -i -X POST http://localhost:7242/_dev/validation/clickhouse-cap
```

Poll the returned `runId`:

```bash
curl --fail http://localhost:7242/_dev/validation/clickhouse-cap/<runId>
```

A successful result should show:

- retry deliveries: 2, successful ClickHouse writes: 1;
- duplicate-event deliveries: 2, successful ClickHouse writes: 2;
- physical ClickHouse rows: 3;
- logical business events: 2, counted with `uniqExact(event_id)`.

The table uses append-only `MergeTree`. It does not enforce a unique key, so duplicate CAP deliveries can create duplicate physical rows. The validation deliberately distinguishes physical writes from logical events. Analytical queries must deduplicate by the stable business `event_id` (for example, `uniqExact(event_id)`) before counting behavior. This is query-level logical deduplication, not exactly-once storage or physical row uniqueness.

This Development-only endpoint publishes synthetic messages to exercise retry and duplicate delivery. It is intentionally separate from the real Category behavior API described below. The separate CAP validation above verifies transactional outbox commit and rollback with MySQL. Whenever a future business mutation and its behavior event must succeed or fail together, both the mutation and CAP publish must share the same CAP-aware database transaction.

### Real Category behavior event ingestion

The Category API exposes `POST /api/categories/{id}/behavior-events` for a trusted upstream caller to submit a behavior event. The endpoint:

1. Validates the event ID, user ID and supported event type (`view`, `click`, `purchase`).
2. Confirms that the target category exists, is enabled and is not logically deleted.
3. Publishes the event through CAP inside a transaction shared with CAP's MySQL outbox storage.
4. Lets the CAP subscriber write the event to ClickHouse through the Framework-registered `IClickHouseClient`.

The API generates the event timestamp and a correlation ID. For compatibility with the current analytics table, the correlation ID is stored in its `run_id` column. The event's stable `event_id` is the logical idempotency key. Reuse it only when retrying the exact same logical event with an identical payload. ClickHouse's append-only `MergeTree` may still contain multiple physical rows for repeated deliveries or repeated HTTP submissions. Analytical aggregation must deduplicate by `event_id`; reusing one ID for different payloads is invalid.

Send a behavior event for an existing, enabled category (replace the user ID with an identity supplied by your trusted caller):

```bash
EVENT_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')

curl -i -X POST http://localhost:7242/api/categories/1/behavior-events \
  -H 'Content-Type: application/json' \
  -d "{
    \"eventId\": \"$EVENT_ID\",
    \"userId\": \"demo-user-1001\",
    \"eventType\": \"view\"
  }"
```

A successful response is `202 Accepted` and contains `eventId`, `correlationId`, `categoryId` and `status: accepted`. Retain the returned correlation ID and inspect the event in ClickHouse:

```bash
docker compose -f dev-environment/docker-compose.yml exec clickhouse \
  clickhouse-client --user framework --password framework_password \
  --query "SELECT event_id, user_id, category_id, event_type, event_time FROM aspros_analytics.category_behavior_events WHERE run_id = '<correlationId>'"
```

To verify logical deduplication, submit the same request again with the same `eventId`. CAP may deliver both accepted messages and ClickHouse may store two physical rows, but the logical event count remains one:

```sql
SELECT
    count() AS physical_rows,
    uniqExact(event_id) AS logical_events
FROM aspros_analytics.category_behavior_events
WHERE event_id = '<eventId>';
```

An unknown, disabled or logically deleted category returns `404`; invalid event payloads return `400`. The current integration service has no authentication middleware, so `userId` in this test contract must be supplied by a trusted caller. A production deployment must derive it from the authenticated principal rather than trusting an arbitrary public request body. Tenant isolation is also not implemented in this minimal Category fixture and must be added before multi-tenant production use.

### Real Category recommendation API

The real recommendation read path consumes the behavior events submitted through
`POST /api/categories/{id}/behavior-events`. Unlike the Development-only personalization
fixture below, it does not generate synthetic events or create test categories.

Request recommendations for a user:

```bash
curl --fail \
  'http://localhost:7242/api/categories/recommendations?userId=demo-user-1001&windowDays=30&take=20'
```

Query parameters:

- `userId`: trusted caller's user identifier (required, maximum 128 characters).
- `windowDays`: behavior lookback window from 1 to 90 days; defaults to 30.
- `take`: maximum returned categories from 1 to 100; defaults to 20.

The service reads enabled, non-deleted Category candidates from MySQL and behavior aggregates
from ClickHouse. It deduplicates physical ClickHouse rows by `event_id` before scoring. The
initial, deliberately transparent business weights are `view=1`, `click=3`, and
`purchase=8`; event influence decays exponentially with a seven-day half-life. These are
baseline policy values for this integration, not learned model parameters.

Two independent lists are fused using the Framework's generic weighted Reciprocal Rank Fusion:
user-specific behavior relevance and global popularity across users. The response includes
the underlying personalized/popularity scores, logical behavior-event count, last behavior
time, fusion score, source count and final rank. A fusion score is not a probability.
Categories with no user history can still enter through popularity. If the selected window
contains no behavior events at all, the service returns eligible categories in stable catalog
order with `recommendationMode: catalog-fallback`.

Example response fields:

```json
{
  "userId": "demo-user-1001",
  "windowDays": 30,
  "recommendationMode": "personalized-and-popular",
  "items": [
    {
      "categoryId": 1,
      "categoryName": "Electronics",
      "personalizedScore": 2.41,
      "behaviorEventCount": 3,
      "popularityScore": 8.72,
      "fusionScore": 0.0325,
      "sourceCount": 2,
      "rank": 1
    }
  ]
}
```

For a useful local check, submit several events for at least two existing categories and
multiple users, then call the recommendation endpoint for each user. The same user should
receive a stronger personalized signal for categories they interacted with, while the
popularity source reflects activity across users. Use distinct EventIds for distinct logical
events; resubmitting an EventId is treated as a duplicate by the aggregation query.

This API is currently an integration endpoint, not production-secure multi-tenant
recommendation. The service has no authentication middleware or tenant model: do not expose
it publicly or trust arbitrary caller-supplied user IDs. Production hosting must derive
identity from authenticated claims and apply tenant/candidate visibility filters before
returning results.

### Minimal per-user category ranking validation

The Development-only personalization validation demonstrates that the same behavior-event dataset can produce different category rankings for different users. It creates or reuses two stable, dedicated validation categories in MySQL, writes a deterministic synthetic event pattern to ClickHouse, and queries the per-user scores. Repeated runs update the same fixture rows instead of selecting arbitrary business categories or creating new random rows.

Start a validation run:

```bash
curl --fail -X POST http://localhost:7242/_dev/validation/personalization
```

The response contains a generated `runId`. Query the rankings:

```bash
curl --fail http://localhost:7242/_dev/validation/personalization/<runId>
```

The sample uses these illustrative weights:

- `view`: 1 point
- `click`: 3 points
- `purchase`: 8 points

User A has stronger activity in the first category; User B has stronger activity in the second. The query first groups by the stable `event_id`, then aggregates scores by `user_id` and `category_id`. The sample deliberately inserts one duplicate physical row: the expected result is 11 physical rows but 10 logical events, without the duplicate increasing the score.

A passing result must show opposite top-ranked categories for the two users. These weights are only a transparent validation fixture, not a production recommendation model. This endpoint remains a deterministic synthetic fixture for validating aggregation and ranking. The real Category behavior ingestion path is documented in the preceding section; this fixture is still useful because it creates controlled user preference distributions without depending on live traffic.

The API produces two independent rankings for each user: personalized relevance (using the Framework's vector cosine similarity) and global popularity (aggregated across the validation users). It combines those ordered lists with `RankingAlgorithms.ReciprocalRankFusion`, so the two sources do not need to share a raw score scale. The response exposes `SimilarityScore`, `FusionScore` (not a probability), `SourceCount` and the final `Rank`. This demonstrates a reusable algorithm integration, not a production recommendation endpoint; candidate recall, tenant/eligibility filtering, event semantics and feature freshness remain Category business responsibilities.


## Framework reference mode

由于服务目前与 Framework 位于同一仓库，项目通过 `ProjectReference` 引用 Framework 源码，便于 RC 阶段同步开发和调试。待 Framework 包发布并确定服务版本策略后，再切换为 NuGet `PackageReference`。

服务的注册顺序有意先调用 `AddCategoryInfrastructure`，再调用 `AddAsprosFramework`。这会先建立 `IDbContext` 到同一 Scoped `CategoryDbContext` 的映射，再扫描业务程序集，确保 Repository 与 UnitOfWork 共享同一个 DbContext。

## Validation scope

当前 Category 字段和自关联关系是用于 Framework CRUD / Dapper 集成验证的最小数据模型，不应视为已经确定的产品类目领域规则。此阶段不实现完整的类目树规则、移动规则或产品业务流程。
