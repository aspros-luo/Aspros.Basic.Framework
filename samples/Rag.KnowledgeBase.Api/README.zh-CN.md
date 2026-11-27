# Aspros 企业知识库 RAG Demo

这是一个独立的 .NET 10 示例应用，消费仓库中的 RAG Core、EF Core 和 Worker Hosting 项目，不把 Demo 业务逻辑放进 Framework 包。

## 当前可体验能力

- 创建知识库，上传 TXT / Markdown 文件（最大 10 MB）。
- SQLite 保存知识库、文档、版本、摄取任务和规范 Chunk 正文。
- 持久化 Job Queue + BackgroundService Worker 异步切片。
- 通过 OpenAI-compatible API 调用真实 Embedding 和 Chat 模型；云端与本地模型分别配置。
- Qdrant 保存向量与 Chunk / Document / IndexGeneration 标识，不保存规范正文。
- 后台任务按“切片 → Embedding → Qdrant Upsert → IndexGeneration 激活”执行。
- 查询时由 Qdrant 召回候选，再回到 SQL 验证文档当前版本和活动索引代次，最后读取规范 Chunk 正文。
- 提供语义检索 API 与带来源片段的 RAG Chat API。

当前仍不包含 PDF / Office 解析、用户认证/ACL、关键词混合检索、Reranker、生产级审计与成本治理。

## 启动依赖

### 1. 启动 Qdrant

在仓库根目录或 Demo 目录执行：

~~~bash
docker compose -f samples/Rag.KnowledgeBase.Api/docker-compose.yml up -d
~~~

Qdrant 默认暴露 REST 6333 和 gRPC 6334，数据持久化在 Docker volume。

### 2. 准备本地模型（默认配置）

安装并启动 Ollama，然后拉取模型：

~~~bash
ollama pull nomic-embed-text
ollama pull qwen2.5:7b
~~~

默认配置通过 Ollama 的 OpenAI-compatible API：
- Embedding：nomic-embed-text
- Chat：qwen2.5:7b

如果本机没有 Ollama，可以将 Embedding 与 Chat 分别切换到任意兼容 OpenAI API 的云端服务。

### 3. 启动 Demo

在仓库根目录执行：

~~~bash
dotnet run --project samples/Rag.KnowledgeBase.Api/Rag.KnowledgeBase.Api.csproj
~~~

服务默认只绑定本机：http://127.0.0.1:7280

首次启动会在示例目录创建隔离的 App_Data/rag-demo.db 和 App_Data/documents/。这里使用 EnsureCreated 仅为方便运行 Demo；真实业务应用仍应自行管理 DbContext、数据库和 Migration。不要把示例数据库或本地文件存储配置直接用于生产。

> 安全边界：当前 Demo 固定使用 demo-tenant，没有用户认证/ACL，只允许本机访问。它用于功能演示，不是可公开部署的多租户服务。

## 云端 / 本地模型切换

Embedding 与 Chat 配置彼此独立，均使用 OpenAI-compatible API。可通过环境变量覆盖 appsettings。

例如将 Embedding 切换到 OpenAI，Chat 继续使用本地 Ollama：

~~~bash
Ai__Embedding__BaseUrl=https://api.openai.com/v1 \
Ai__Embedding__ApiKey="$OPENAI_API_KEY" \
Ai__Embedding__Model=text-embedding-3-small \
dotnet run --project samples/Rag.KnowledgeBase.Api/Rag.KnowledgeBase.Api.csproj
~~~

将 Chat 切换到云端时，设置 Ai__Chat__BaseUrl、Ai__Chat__ApiKey 和 Ai__Chat__Model。如果云服务商提供 OpenAI-compatible 接口，只需修改对应 BaseUrl 和模型名。不要把真实 API Key 写入仓库配置文件。

切换 Embedding 模型或向量维度后，Demo 会按模型标识和向量维度使用独立 Qdrant collection。注意：当前 Demo 的一个知识库应统一使用同一 Embedding Profile；切换模型属于全量索引迁移，必须重新摄取该知识库全部文档。迁移完成前，未重建的旧模型索引不会出现在新模型的查询结果中。演示时建议为不同 Embedding 模型分别创建知识库。生产系统应把 Embedding Profile 固化在知识库/索引代次配置中，并支持按 Profile 路由查询。

## 快速体验

### 1. 创建知识库

~~~bash
curl -sS -X POST http://127.0.0.1:7280/api/knowledge-bases \
  -H 'Content-Type: application/json' \
  -d '{"name":"产品与工程知识库"}'
~~~

从响应中复制 id。

### 2. 上传文档

~~~bash
curl -sS -X POST \
  http://127.0.0.1:7280/api/knowledge-bases/<knowledgeBaseId>/documents \
  -F 'file=@./README.md'
~~~

响应会包含 documentId、versionId 和 jobId。上传成功只代表文件与摄取任务已持久化，后台 Worker 随后执行切片和向量索引。

### 3. 查看任务状态

~~~bash
curl -sS http://127.0.0.1:7280/api/ingestion-jobs/<jobId>
~~~

等待 status 变为 Succeeded。成功日志会包含 Chunk 数、Embedding 模型、向量维度和激活的 IndexGeneration。

### 4. 查看规范 Chunk

~~~bash
curl -sS http://127.0.0.1:7280/api/documents/<documentId>/chunks
~~~

### 5. 执行语义检索

~~~bash
curl -sS -X POST \
  http://127.0.0.1:7280/api/knowledge-bases/<knowledgeBaseId>/search \
  -H 'Content-Type: application/json' \
  -d '{"query":"向量数据库保存什么数据？","take":5}'
~~~

返回结果包含相似度分数、文档、Chunk 和来源位置。正文来自 SQL，而不是 Qdrant payload。

### 6. 进行带来源的 RAG 问答

~~~bash
curl -sS -X POST \
  http://127.0.0.1:7280/api/knowledge-bases/<knowledgeBaseId>/chat \
  -H 'Content-Type: application/json' \
  -d '{"question":"描述一下当前 RAG 的存储架构。","take":5}'
~~~

响应包含模型回答和实际送入上下文的来源 Chunk。Prompt 要求模型只依据检索资料回答；资料不足时应明确说明。

## API

| Method | Route | 用途 |
|---|---|---|
| GET | /health | 健康检查 |
| POST | /api/knowledge-bases | 创建知识库 |
| GET | /api/knowledge-bases | 列出知识库 |
| POST | /api/knowledge-bases/{id}/documents | 上传 TXT / Markdown |
| GET | /api/knowledge-bases/{id}/documents | 文档与 Chunk 数量 |
| GET | /api/ingestion-jobs/{id} | 摄取任务状态 |
| GET | /api/documents/{id}/chunks | 查看规范 Chunk 正文 |
| POST | /api/knowledge-bases/{id}/search | 语义检索 |
| POST | /api/knowledge-bases/{id}/chat | 带来源的 RAG 问答 |
| GET | /openapi/v1.json | OpenAPI 描述（Development 环境） |

## 当前实现边界

- 真实模型调用：不是 Fake Embedding 或静态答案。
- Qdrant 只存向量和标识，SQL 是正文、版本和活动索引状态的事实来源。
- 每个 IngestionJob 最多关联一个 IndexGeneration；重试复用同一代，激活采用已有 EF Core CAS 激活器。
- 查询结果必须通过 SQL 校验 Document 未删除、Chunk 属于当前文档版本且 IndexGeneration 仍为活动代次。
- 当前使用单一 Demo Tenant，不代表生产环境已完成 ACL 隔离。
- Qdrant 当前为本地单节点 Demo 部署；生产环境需补充认证、备份、监控、索引清理和高可用策略。
