using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace Rag.KnowledgeBase.Api;

public sealed record DemoVectorMatch(Guid ChunkId, Guid IndexGenerationId, float Score);

public sealed class DemoVectorStore(QdrantClient client)
{
    public async Task UpsertAsync(
        string tenantId,
        Guid knowledgeBaseId,
        Guid documentId,
        Guid documentVersionId,
        Guid indexGenerationId,
        string embeddingModelKey,
        IReadOnlyList<Guid> chunkIds,
        IReadOnlyList<float[]> vectors,
        CancellationToken cancellationToken)
    {
        if (chunkIds.Count != vectors.Count || vectors.Count == 0)
        {
            throw new ArgumentException("Chunk IDs and vectors must be non-empty and have the same length.");
        }

        var dimension = vectors[0].Length;
        if (vectors.Any(vector => vector.Length != dimension))
        {
            throw new ArgumentException("All vectors in a batch must have the same dimension.");
        }

        var collection = GetCollectionName(embeddingModelKey, dimension);
        if (!await client.CollectionExistsAsync(collection, cancellationToken))
        {
            try
            {
                await client.CreateCollectionAsync(
                    collection,
                    new VectorParams { Size = (ulong)dimension, Distance = Distance.Cosine },
                    cancellationToken: cancellationToken);
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
            {
                // Another worker created the same model/dimension collection concurrently.
            }
        }

        var points = new List<PointStruct>(vectors.Count);
        for (var index = 0; index < vectors.Count; index++)
        {
            points.Add(new PointStruct
            {
                Id = chunkIds[index],
                Vectors = vectors[index],
                Payload =
                {
                    ["tenant_id"] = tenantId,
                    ["knowledge_base_id"] = knowledgeBaseId.ToString("D"),
                    ["document_id"] = documentId.ToString("D"),
                    ["document_version_id"] = documentVersionId.ToString("D"),
                    ["chunk_id"] = chunkIds[index].ToString("D"),
                    ["index_generation_id"] = indexGenerationId.ToString("D")
                }
            });
        }

        await client.UpsertAsync(collection, points, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DemoVectorMatch>> SearchAsync(
        string tenantId,
        Guid knowledgeBaseId,
        string embeddingModelKey,
        float[] vector,
        int take,
        CancellationToken cancellationToken)
    {
        var collection = GetCollectionName(embeddingModelKey, vector.Length);
        if (!await client.CollectionExistsAsync(collection, cancellationToken))
        {
            return [];
        }

        var filter = new Filter
        {
            Must =
            {
                MatchKeyword("tenant_id", tenantId),
                MatchKeyword("knowledge_base_id", knowledgeBaseId.ToString("D"))
            }
        };

        var points = await client.SearchAsync(
            collection,
            vector,
            filter: filter,
            limit: (ulong)Math.Clamp(take, 1, 50),
            cancellationToken: cancellationToken);

        var matches = new List<DemoVectorMatch>(points.Count);
        foreach (var point in points)
        {
            if (!point.Payload.TryGetValue("chunk_id", out var chunkValue) ||
                !Guid.TryParse(chunkValue.StringValue, out var chunkId) ||
                !point.Payload.TryGetValue("index_generation_id", out var generationValue) ||
                !Guid.TryParse(generationValue.StringValue, out var generationId))
            {
                continue;
            }

            matches.Add(new DemoVectorMatch(chunkId, generationId, point.Score));
        }

        return matches;
    }

    private static string GetCollectionName(string embeddingModelKey, int dimension)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(embeddingModelKey)))[..12]
            .ToLowerInvariant();
        return $"aspros_rag_{dimension}_{hash}";
    }
}
