# 企业知识库 RAG 架构决策

> 状态：Accepted（截至 2026-11-27，Phase 1 关系型持久化、任务租约、索引代次幂等与原子切换已通过用户本地 Release 定向测试；可选 Worker Hosting 基础已实现，端到端摄取流程仍未实现。仅在 feature/enterprise-rag-foundation 开发，不改变 2.0.0 RC / master）
>
> 适用范围：Aspros.Basic.Framework，目标框架 .NET 10。
>
> 本文是企业知识库 RAG 的唯一中文架构决策入口。后续实现细节应更新本文或对应代码注释，不另建重复的平行方案文档。

## 1. 背景与目标

首个 AI 场景确定为企业知识库 RAG；模型部署必须同时支持云端 API 与本地模型。存储采用分离架构：

- 关系型数据库管理知识库业务事实、文档、权限、版本、Chunk 内容、索引状态和摄取任务。
- 向量数据库只承担派生检索索引，不作为文档、权限或任务状态的事实来源。
- 原始 PDF、Office 文件等二进制内容放在对象存储或受控文件存储；关系型数据库保存 URI、Hash、版本和元数据。
- 模型 Provider 与 Vector Store Provider 均可替换。

本次设计不包含通用 Agent 平台、模型训练、复杂工作流引擎，也不改变当前 2.0.0 RC 的发布范围。

## 2. 基于现有仓库的约束

当前 RC 分支为 `release/v2.0.0-rc.1`，与 master 比较为 ahead 273、behind 0。master 当前不包含 RC 新增的 Algorithms 与 ClickHouse 项目；AI 开发必须以 RC 稳定发布并进入后续开发基线为前提，不向现有六个 RC 包注入 AI 依赖。

已检查的 RC 结构：

- `Aspros.Base.Framework.Infrastructure` 已使用 EF Core 10.0.15、Dapper、CAP、HTTP resilience 等，但它的职责是通用基础设施，不应承载 RAG 领域模型或模型供应商 SDK。
- `Aspros.Base.Framework.Algorithms` 是无外部包依赖的独立算法项目；`RankingAlgorithms.ReciprocalRankFusion` 已实现加权 RRF，并有集成测试。RAG 应复用它，不复制算法。
- `Aspros.Base.Framework.ClickHouse` 目前只提供 ClickHouse.Driver 客户端注册与健康检查，不是向量存储实现。不得因为项目已存在就假设它具备 Vector Store 能力。
- Category 示例由业务服务持有自己的 EF Core DbContext 和实体映射。RAG 持久化集成应允许业务服务继续拥有 DbContext 和 Migration。

历史 AI 实现中的 trigram hash 假 Embedding、静态 JSON 向量文件、模拟 Agent Loop 和内存模拟 ClickHouse 不作为新实现基础。

## 3. 架构决策

### 3.1 数据所有权

| 数据 | 权威位置 | 说明 |
|---|---|---|
| Tenant、KnowledgeBase | 关系型数据库 / 业务域 | 租户和知识库归属 |
| Document、DocumentVersion | 关系型数据库 | 文档元数据、版本、Hash、状态 |
| 原始文件 | 对象存储 / 文件存储 | PDF、DOCX、Markdown 等原始内容 |
| DocumentChunk | 关系型数据库 | Chunk 文本、序号、页码/段落位置、来源元数据 |
| 权限与授权关系 | 业务授权服务 / 关系型数据库 | Framework 不成为 ACL 权威 |
| IngestionJob、IndexGeneration | 关系型数据库 | 任务状态、租约、重试、索引代次 |
| Embedding 与向量索引 | 向量数据库 | 只存可重建的向量记录和过滤所需的冗余元数据 |
| 检索候选、RRF 分数、上下文 | 请求内存 | 临时计算结果 |

向量记录至少包含 ChunkId、TenantId、KnowledgeBaseId、DocumentId、DocumentVersion、IndexGenerationId、EmbeddingModelKey、EmbeddingDimension。除 ChunkId 与向量外，过滤字段是关系型数据的非权威副本，用于在向量检索阶段执行过滤。

向量数据库不保存唯一的 Chunk 正文。向量检索返回 ChunkId 后，从关系型数据库读取规范文本、来源位置和当前文档状态，再组装上下文与引用。

### 3.2 一致性模型

采用“关系型数据库提交业务事实，向量索引最终一致”的模型。不引入跨数据库分布式事务。

文档写入流程：

1. 在同一关系型数据库事务中写入 DocumentVersion、Chunk（处理完成后）及 IngestionJob。
2. Worker 以租约方式领取任务，解析文档、切片并生成 Embedding。
3. 向量写入使用确定性幂等键：TenantId + DocumentId + DocumentVersion + ChunkingProfileVersion + EmbeddingModelKey + IndexGenerationId。
4. 写入完成后验证记录数、维度和必要元数据。
5. 在关系型数据库事务中切换 ActiveIndexGenerationId。
6. 旧代索引异步清理；清理失败不影响当前活动索引。

更新文档时先构建新版本/新代索引，验证完成后再切换活动指针。删除文档时先在关系型数据库中将其标记为不可检索/删除，再异步清理向量记录。不能先删除旧索引再构建新索引。

Worker 任务需要 Pending、Processing、Succeeded、Failed 等状态，包含 LeaseOwner、LeaseExpiresAt、AttemptCount、NextAttemptAt、LastError、CreatedAt、UpdatedAt。领取任务必须是数据库条件更新/并发安全操作；不能依赖“先 SELECT 再 UPDATE”而没有并发保护。

第一阶段使用关系型数据库中的持久化 Job 表作为任务队列，不强制引入 CAP、RabbitMQ 或独立 Worker 服务。后续只有在吞吐量、处理时长或故障隔离有真实需求时才引入消息传输。

### 3.3 权限安全

Framework 不定义企业的用户、角色、部门或 ACL 体系。业务服务负责身份认证和授权，并向 RAG 提供不可伪造的授权上下文（TenantId、KnowledgeBase 范围、文档范围或权限标签）。

检索必须满足：

1. 请求必须提供授权上下文；缺失时拒绝执行，不能默认全库可见。
2. 向量查询必须带 TenantId、KnowledgeBaseId、ActiveIndexGenerationId 等强制过滤条件；若使用文档级 ACL，必须把已授权文档 ID 或权限标签映射为 Provider 可执行的前置过滤条件。
3. 向量 Provider 必须明确声明过滤能力。严格 ACL 模式下不支持必要过滤的 Provider 不得静默降级为无过滤检索。
4. 命中的 ChunkId 必须回到关系型数据库读取规范正文，并再次检查文档版本、删除状态和当前权限。
5. 只有最终授权通过的 Chunk 才能进入 Reranker、LLM Prompt 或 Citation。
6. 权限变化必须影响下一次查询；不能只依赖文档摄取时复制的 ACL 元数据。

向量过滤前置执行用于避免无权数据挤占 Top-K；关系型数据库二次授权用于防止索引元数据过期。两层都不能省略。

### 3.4 索引版本与模型变更

Embedding 模型、维度、Chunking 策略、规范化规则均属于索引兼容性的一部分。必须记录：

- Provider 与模型标识；
- Embedding Dimension；
- ChunkingProfileVersion；
- DocumentVersion 与 ContentHash；
- IndexGenerationId；
- 创建时间和索引状态。

不同 Embedding 模型产生的向量不得因为维度相同就混合检索。模型升级采用新代索引构建、验证、切换和延迟清理的蓝绿流程。

## 4. 包拆分与依赖

不为了包装微软接口而创建重复抽象，也不一次性拆出大量小包。

### 4.1 第一阶段候选包

| 包 | 职责 | 依赖 |
|---|---|---|
| `Aspros.Base.Framework.RAG` | RAG 核心契约与 Pipeline：文档/Chunk 模型、摄取编排、检索编排、上下文、引用、权限上下文、索引代次 | Phase 1 无第三方依赖；Phase 2 按需加入 MEAI / VectorData 抽象；Phase 4 再引用 Algorithms |
| `Aspros.Base.Framework.RAG.EntityFrameworkCore` | 关系型元数据、Chunk、Job、索引代次的 EF Core 映射和持久化实现 | RAG、`Microsoft.EntityFrameworkCore.Relational` 10.x |
| `Aspros.Base.Framework.RAG.Hosting` | 可选 BackgroundService 宿主：任务轮询、并发执行、租约续期、取消失去租约的处理器、完成/失败回写 | RAG、Microsoft.Extensions.Hosting / Options / Logging abstractions 10.x |

RAG 包不依赖 Infrastructure，也不要求业务服务使用 Framework 自己的 DbContext。Phase 1 的核心包保持零第三方依赖；AI 与 VectorData 抽象在 Phase 2 真正实现模型/向量能力时再加入，RRF 的 Algorithms 引用延后到 Phase 4。EF Core 包提供实体配置/存储实现接入点；业务服务仍负责把配置纳入自己的 DbContext、连接字符串和 Migration。

`Aspros.Base.Framework.AI` 暂不作为首期必建包。RAG 直接使用微软的 `IChatClient` 与 `IEmbeddingGenerator` 标准抽象。只有当 RAG 之外出现多个真实 AI 场景，并且模型路由、策略、用量治理、审计或统一遥测形成跨场景复用能力时，再新增 AI 包。否则单独建 AI 包只是对标准接口的薄包装。

### 4.2 Provider 策略

- 云端和本地模型使用相同的 Microsoft.Extensions.AI 抽象；Chat 与 Embedding Provider 可以独立选择。
- OpenAI、Azure、Ollama 或 OpenAI-compatible 服务优先使用经过验证的 Provider 适配器，不为每个厂商都创建 Aspros 包。
- RAG 通过 VectorData 抽象访问向量集合；具体 Vector Store 由业务应用在 Composition Root 注册。
- 不立即创建 `RAG.ClickHouse`。当前 ClickHouse 包只提供客户端与健康检查；只有 ClickHouse VectorData Connector 通过真实功能、权限过滤、维度、批量写入、更新/删除和性能契约测试后，才考虑 Aspros 专用适配包。
- 不支持的 Provider 能力必须在启动验证或查询时显式失败，不能静默忽略过滤条件。

### 4.3 版本基线与预览依赖

目标为 .NET 10。2026-11-27 核查到：

- `Microsoft.Extensions.AI.Abstractions` 与 `Microsoft.Extensions.VectorData.Abstractions` 已有 10.11.1 稳定版本，可作为实现阶段的候选版本基线；实现前重新检查兼容性与安全更新。
- `Microsoft.Extensions.DataIngestion` 的 .NET 10 包仍是 preview 版本。因此不得把它作为首期公开稳定 RAG 包的强制传递依赖。先定义小而稳定的 Aspros 文档摄取契约；可在内部原型或隔离适配器中评估 DataIngestion，待其稳定后再决定是否集成。
- EF Core 版本应与现有 .NET 10 / EF Core 10 依赖线对齐；具体补丁版本在实施时统一核对。

仓库当前各项目在 csproj 中显式指定包版本。本次不为了 RAG 顺带迁移全仓到 Central Package Management；新项目沿用当前风格，避免无关改动。

## 5. 组件职责

### RAG 核心

- Document ingestion contract：接收文档来源、内容流、业务元数据和版本信息。
- Parser adapter：将 PDF、DOCX、HTML、Markdown 等内容转为统一文本块；格式解析器按需单独适配。
- Chunker：按 Token/字符预算、段落边界和重叠策略切片，保留页码、标题、段落位置。
- Embedding：通过 `IEmbeddingGenerator<string, Embedding<float>>` 生成向量。
- Vector index：写入/删除向量，执行带强制过滤条件的近邻查询。
- Keyword retrieval：通过独立接口接入关系型全文检索或其他全文检索引擎。
- Fusion：复用 `RankingAlgorithms.ReciprocalRankFusion`。
- Reranking：可选接口，不强制绑定某个模型。
- Context assembly：去重、按文档/Token 预算组装上下文。
- Citation：返回 DocumentId、DocumentVersion、ChunkId、标题、页码/位置和来源信息。

### 业务服务

- 用户身份认证、租户归属和 ACL 策略；
- 知识库创建与管理；
- 文档上传入口、原始文件存储；
- 文档业务生命周期与授权决策；
- 数据库连接、DbContext 和 Migration；
- Prompt 模板和回答产品策略；
- 对外 API、审计和业务限额。

### AI Provider

模型接入由应用组合根注册。模型供应商的速率限制、上下文长度、批量大小、超时和流式能力不能假设一致，必须通过 Provider 契约测试和配置约束表达。

## 6. 查询流程

1. 业务服务认证请求并生成 RagAccessScope。
2. 校验 Tenant、KnowledgeBase、授权范围和请求预算。
3. 使用 Embedding Provider 将查询转为向量。
4. Vector Store 在 Tenant、KnowledgeBase、活动索引代次和授权标签约束下召回候选 ChunkId。
5. Keyword Search 可并行召回候选。
6. 复用 Algorithms 中的加权 RRF 合并候选。
7. 从关系型数据库批量读取 Chunk 正文和 Document 元数据。
8. 再次验证活动版本、删除状态和当前 ACL。
9. 可选 Reranker 排序。
10. Context Builder 按 Token 预算组装上下文，保留来源位置。
11. Chat Provider 生成回答。
12. 返回 Answer、Citations、模型/检索元数据；不得把未授权 Chunk 暴露给模型或客户端。

## 7. 安全与可靠性非功能要求

- Prompt Injection：检索文档是非可信数据，不得把文档中的指令当作系统指令；第一阶段 RAG 只读，不开放 Agent 工具执行。
- 数据隔离：所有检索入口强制 Tenant/KnowledgeBase 过滤；禁止默认全局搜索。
- 数据删除：关系型状态先禁止检索，向量删除异步完成；记录清理结果并支持重试。
- 幂等：重复上传、重复任务、Worker 重启和重复向量写入不产生多份有效索引。
- 可恢复：Worker 使用租约和心跳；过期租约可重新领取；失败任务按策略重试，超过上限进入终止状态并保留错误。
- 可观测：记录 ingestion、embedding、vector search、keyword search、fusion、rerank、generation 的耗时和失败原因；敏感文档正文、Prompt、密钥默认不写入日志。
- 成本：记录模型、Token Usage、耗时和 Provider；价格估算作为可配置数据，不把价格硬编码到模型调用核心。
- 质量：维护固定评测集，至少衡量 Recall@K、MRR、Citation Precision/Recall、回答有据率和跨租户泄漏用例。

## 8. 实施顺序与验收门槛

AI/RAG 实施不阻塞 2.0.0 稳定版发布。当前 RC 的验收、合并与发布仍独立进行；Phase 1 在 feature/enterprise-rag-foundation 上开发，基于 RC 代码，不修改 RC 或 master。待 2.0.0 稳定版基线确定后再进行兼容性复核与合并。

### Phase 0：发布基线

- 完成六个 RC 包的稳定发布。
- 将已验收 RC 合并进入正式主线。
- 以稳定版代码为 AI 实施基线。
- 核对现有 Algorithms、ClickHouse、Infrastructure 的最终 API 与依赖。

### Phase 1：最小 RAG 契约与关系型持久化

实施范围：
- 已建立 `Aspros.Base.Framework.RAG` 与 `Aspros.Base.Framework.RAG.EntityFrameworkCore` 两个 preview 项目。
- 已定义 KnowledgeBase、Document、DocumentVersion、DocumentChunk、IngestionJob、IndexGeneration；Document 通过 CurrentVersionId 指向最新已就绪文档版本，通过 ActiveIndexGenerationId 独立指向当前可检索的活动索引代次。设置新版本时不清空旧活动索引指针，因此重建失败或进行中时旧索引仍可用；只有新代索引构建并验证成功后，应用层才在同一关系型事务中切换 ActiveIndexGenerationId，并将旧代标记为 Retired。激活索引时校验其属于当前最新版本、同一文档、租户和知识库。查询必须以 ActiveIndexGenerationId 对应的代次为准，不能简单要求活动索引版本始终等于最新文档版本。
- EF Core 包通过 `ModelBuilder.ApplyRagModel()` 将映射加入业务服务自有 DbContext；业务服务继续负责连接、DbContext 与 Migration。
- 已实现基于条件更新和 LeaseToken 的任务租约，包含领取、续租、完成、失败重试与终止失败；租约 Token 防止旧 Worker 在接管后提交完成状态。
- 新增可选 `Aspros.Base.Framework.RAG.Hosting` 包。业务应用实现 `IIngestionJobHandler` 并调用 `AddRagWorker<THandler>()` 即可启用后台 Worker；队列存储仍需由应用单独注册（例如 `AddRagEntityFrameworkCore<TContext>()`）。Worker 支持配置并发度、轮询间隔、租约时长、心跳间隔和指数退避；`IngestionJobPermanentException` 表示不可重试的永久错误。
- Worker 对每次队列操作、每个任务处理器和每次租约心跳分别创建 DI Scope，避免 handler 与 heartbeat 并发使用同一个 Scoped DbContext。租约续期失败或心跳异常时会取消 handler，并且不再使用失效租约写入完成/失败状态；宿主关闭时停止处理并让未完成租约自然过期，由后续 Worker 接管。
- 任务轮询在确有“最后一次尝试已过期”的任务时才执行终止恢复写入；无待恢复任务时只执行存在性查询，避免每个 Worker 的空更新在高频轮询时制造不必要的数据库写锁竞争。
- IngestionJob 增加租户范围内唯一的 IdempotencyKey，避免同一请求重复创建任务。
- 每个 IndexGeneration 必须关联一个 IngestionJob；通过 IngestionJobId 唯一索引保证一个任务最多产生一个索引代次，并通过包含 Tenant/KnowledgeBase/Document/DocumentVersion 的复合外键保证任务归属一致。相同文档版本与索引配置下，显式重新索引必须创建新任务，因此允许创建新代次，不会被错误的“配置唯一键”拦截。
- 新增 EF Core 索引代次激活器：在关系型事务内先按 DocumentId + 观察到的 CurrentVersionId + ActiveIndexGenerationId 执行条件更新（CAS），只有成功取得文档行更新权的执行者才能切换活动指针；随后保存新代 Activate 与旧代 Retire。并发竞争失败返回 false，事务回滚，不会把未获胜代次标为 Active；如果 Worker 在指针切换后、任务完成前崩溃，重试激活同一代次会幂等返回成功。
- UTC 时间字段通过 UTC ticks 转换存储，避免 SQLite 等 Provider 对 DateTimeOffset 范围查询的限制。
- KnowledgeBase 以 (TenantId, Id) 建立备用键；Document、Chunk、IndexGeneration 和 IngestionJob 通过复合外键校验租户与知识库归属，Chunk、IndexGeneration 和 IngestionJob 还会校验 DocumentVersion 必须属于对应 Document。
- SQLite 集成测试覆盖关系型 Chunk 正文持久化、跨租户知识库关联拒绝、蓝绿索引切换及失败时保留旧活动指针、关系型事务持久化切换、租户内任务幂等键唯一性、顺序领取、8 个独立 DbContext/连接并发竞争同一任务且仅一个获得租约、续租、租约过期接管、旧租约的续租/失败/完成操作全部失效、失败重试上限，以及最后一次尝试后 Worker 崩溃的终止恢复。SQLite 并发测试使用文件数据库和独立连接。

本地验证（2026-11-27）：
- `dotnet build Aspros.Base.Framework.sln --configuration Release`：成功；全 Solution 报告 187 条 warning，需单独分类治理，不影响本次编译成功结论。
- 用户本轮本地执行 `RagPersistenceTests`：15 项通过，0 失败，0 跳过；包含 EF Core DI 注册、8 个独立 DbContext/连接并发领取竞争，以及蓝绿索引切换和关系型事务持久化测试。
- 新增 `RagMySqlProviderTests`：通过独立的 `ConnectionStrings__RagTestDatabase` 环境变量启用，使用专用 RAG 测试数据库验证 8 个独立 DbContext 并发领取同一任务仅有一个租约；另增加租约过期接管测试，模拟 Worker 进程失联后由新 Worker 重新领取，并验证旧 LeaseToken 无法续租、失败或完成任务。未配置该变量时自动 Skip；不使用或清理共享的 `ConnectionStrings__TestDatabase`，避免破坏 Framework/Category 测试数据。用户本地三项 MySQL Provider 测试均已通过（3/3，0 失败，0 跳过）。
- MySQL Provider 测试建议先创建独立数据库 `aspros_framework_rag_test`，并将 `ConnectionStrings__RagTestDatabase` 指向该库；例如：`Server=127.0.0.1;Port=13306;Database=aspros_framework_rag_test;User=framework;Password=framework_password;`。该账号需要该独立数据库的建表、删表权限（测试会重建专用 RAG 表结构）。运行命令：`dotnet test --project tests/Aspros.Basic.Framework.IntegrationTests/Aspros.Basic.Framework.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~RagMySqlProviderTests"`。
- RAG Core 与 EF Core 集成项目均在 `net10.0` Release 配置下成功生成。

仍未实现或验证：
- Document.CurrentVersionId 与 ActiveIndexGenerationId 是有意分离的状态：新版本发布后旧活动代次继续服务，直到新代验证成功并在事务内切换指针。当前 SQLite 与 MySQL 回归测试覆盖重建失败保留旧指针、成功后切换新代并退役旧代，以及两个独立 DbContext 并发激活时单一 CAS 获胜；更广泛的并发读写快照和生产负载行为仍待验证。CurrentVersionId 当前由领域方法维护，尚未配置数据库外键约束，需在真实 Provider/迁移设计中评估一致性边界。
- 尚未接入真实业务服务 DbContext/Migration；业务服务接入示例目前只验证模型配置方式，未证明任一真实业务数据库迁移可用。
- Worker Hosting 已新增六项集成测试：独立 DI Scope、SQLite + EF Core 队列领取/续租/完成、租约丢失时取消 handler 且不回写、瞬时异常重试退避、永久异常终止失败、宿主优雅关闭时不错误完成任务。以上新增测试尚待用户本地执行；真实进程崩溃和多实例端到端行为仍未验证。文档解析、Chunking Pipeline、Embedding、Vector Store 与索引构建编排仍未实现。
- 尚未进行独立 Worker 进程/多实例端到端测试，也未验证真实进程崩溃后的自动恢复；目前通过过期租约、独立 DbContext 和模拟时钟验证了数据库层接管逻辑。
- 已在 MySQL 8.4 上验证任务租约领取、过期接管和并发索引激活/CAS。新增 Worker Hosting 后尚未进行 Release build/test；代码需由用户本地验证。UTC ticks 转换、复合外键在当前测试路径中已随 MySQL 模型创建/写入经过基础验证，但尚未覆盖全部关系与查询组合。

Phase 1 后续验收门槛：并发领取测试通过；在目标 MySQL Provider 上验证多 Worker 并发下同一任务只有一个有效租约；真实 Worker 重启后任务可恢复；同一 IngestionJob 不会创建重复索引代次；相同配置的显式重建可创建新代次；MySQL 多 Worker 并发激活时仅一个 CAS 获胜且只有一个 Active 代次；关系型规范数据可用于完整重建向量索引。

### Phase 2：模型与向量索引

- 接入一个云端 Chat/Embedding Provider。
- 接入一个本地 Chat/Embedding Provider。
- 接入一个经过契约验证的 Vector Store Provider。
- 建立模型/维度/索引代次校验。
- 实现新代索引构建和原子切换。

验收：切换云端/本地模型不改变 RAG 业务逻辑；模型维度不匹配时拒绝写入；索引重建期间旧索引仍可查询。

### Phase 3：安全检索与引用

- 实现强制 Tenant/KnowledgeBase/ACL 过滤。
- 加入关系型正文回读和二次授权。
- 返回可追溯 Citation。
- 建立跨租户、ACL 变化、文档删除和索引滞后测试。

验收：无权 Chunk 不进入 LLM Prompt，也不出现在 Citation；严格权限模式下 Provider 不支持必要过滤时必须失败关闭。

### Phase 4：检索质量

- 增加关键词召回。
- 复用 RRF，不复制算法。
- 增加可选 Reranker。
- 建立固定评测集和 Recall@K、MRR、Citation 指标。

验收：每次修改 Chunking、Embedding、Fusion 或 Reranker 都能在同一评测集上比较。

### Phase 5：生产化与 Provider 扩展

- 完善限流、成本、Telemetry、任务运维和索引清理。
- 验证 ClickHouse VectorData Connector 与至少一个替代向量 Provider。
- 依据实测负载决定是否引入 MQ、独立 Worker 或服务拆分。

验收：故障可恢复、索引可重建、Provider 可替换，且有可解释的性能和质量基线。

## 9. 明确暂缓

- 通用 Agent、工具调用工作流和自主执行。
- 将 AI SDK 或 Provider 依赖加入现有 Infrastructure。
- 将 ClickHouse 设为唯一向量数据库。
- 将预览版 DataIngestion 作为公开稳定包的强制依赖。
- 一开始拆分 Ingestion、Retrieval、Generation 为多个 NuGet 包或微服务。
- Framework 自己定义用户、角色、部门、租户授权体系。
- 在没有检索评测证据前宣称“准确率提升”或“生产级 RAG”。

## 10. 参考

- Microsoft.Extensions.AI：<https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai>
- Microsoft.Extensions.VectorData：<https://learn.microsoft.com/dotnet/ai/vector-stores>
- Microsoft.Extensions.DataIngestion：<https://learn.microsoft.com/dotnet/ai/data-ingestion>
- NuGet Microsoft.Extensions.AI.Abstractions：<https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions>
- NuGet Microsoft.Extensions.VectorData.Abstractions：<https://www.nuget.org/packages/Microsoft.Extensions.VectorData.Abstractions>
- NuGet Microsoft.Extensions.DataIngestion：<https://www.nuget.org/packages/Microsoft.Extensions.DataIngestion>
