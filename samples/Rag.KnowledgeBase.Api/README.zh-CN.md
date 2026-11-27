# Aspros 企业知识库 RAG Demo

这是一个独立的 .NET 10 示例应用，消费仓库中的 RAG Core、EF Core 和 Worker Hosting 项目，不把 Demo 业务逻辑放进 Framework 包。

## 当前可体验能力

- 创建知识库。
- 上传 TXT / Markdown 文件（最大 10 MB）。
- 使用 SQLite 保存知识库、文档、版本、摄取任务和规范 Chunk 文本。
- 由持久化 Job Queue + BackgroundService Worker 异步领取任务。
- 按段落和字符预算切片，支持查询任务状态、文档列表和 Chunk 内容。
- Worker 使用独立 DI Scope；Chunk 替换在一个关系型数据库事务中完成，可安全重试。

当前阶段**尚未实现** PDF / Office 解析、Embedding、向量存储、语义检索和 LLM 问答。API 会明确报告 Chunk 数量，不会伪造“已建立向量索引”。

## 运行

在仓库根目录执行：

```bash
dotnet run --project samples/Rag.KnowledgeBase.Api/Rag.KnowledgeBase.Api.csproj
```

服务默认只绑定本机：<http://127.0.0.1:7280>

首次启动会在示例目录创建隔离的 `App_Data/rag-demo.db` 和 `App_Data/documents/`。这里使用 `EnsureCreated` 仅为方便运行 Demo；真实业务应用仍应自行管理 DbContext、数据库和 Migration。不要把示例数据库或本地文件存储配置直接用于生产。

> 安全边界：当前 Demo 固定使用 `demo-tenant`，没有用户认证/ACL，只允许本机访问。它用于功能演示，不是可公开部署的多租户服务。

## 快速体验

### 1. 创建知识库

```bash
curl -sS -X POST http://127.0.0.1:7280/api/knowledge-bases \
  -H 'Content-Type: application/json' \
  -d '{"name":"产品与工程知识库"}'
```

从响应中复制 `id`。

### 2. 上传文档

```bash
curl -sS -X POST \
  http://127.0.0.1:7280/api/knowledge-bases/<knowledgeBaseId>/documents \
  -F 'file=@./README.md'
```

响应会包含 `documentId`、`versionId` 和 `jobId`。上传成功只代表文件与摄取任务已持久化，后台 Worker 随后处理文档。

### 3. 查看任务状态

```bash
curl -sS http://127.0.0.1:7280/api/ingestion-jobs/<jobId>
```

等待 `status` 变为 `Succeeded`。

### 4. 查看知识库文档与切片

```bash
curl -sS http://127.0.0.1:7280/api/knowledge-bases/<knowledgeBaseId>/documents
curl -sS http://127.0.0.1:7280/api/documents/<documentId>/chunks
```

## API

| Method | Route | 用途 |
|---|---|---|
| GET | `/health` | 健康检查 |
| POST | `/api/knowledge-bases` | 创建知识库 |
| GET | `/api/knowledge-bases` | 列出知识库 |
| POST | `/api/knowledge-bases/{id}/documents` | 上传 TXT / Markdown |
| GET | `/api/knowledge-bases/{id}/documents` | 文档与 Chunk 数量 |
| GET | `/api/ingestion-jobs/{id}` | 摄取任务状态 |
| GET | `/api/documents/{id}/chunks` | 查看规范 Chunk 文本 |
| GET | `/openapi/v1.json` | OpenAPI 描述（Development 环境） |

下一阶段在此 Demo 上接入真实 Embedding 与可替换向量存储，再实现受授权上下文约束的检索和带引用的模型回答。
