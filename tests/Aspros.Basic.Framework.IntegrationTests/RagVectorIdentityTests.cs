using Rag.KnowledgeBase.Api;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RagVectorIdentityTests
{
    [Fact]
    public void CreatePointId_IsStableForSameGenerationAndChunk()
    {
        var generationId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();

        var first = DemoVectorStore.CreatePointId(generationId, chunkId);
        var retry = DemoVectorStore.CreatePointId(generationId, chunkId);

        Assert.Equal(first, retry);
    }

    [Fact]
    public void CreatePointId_SeparatesSameChunkAcrossIndexGenerations()
    {
        var chunkId = Guid.NewGuid();
        var oldGenerationId = Guid.NewGuid();
        var newGenerationId = Guid.NewGuid();

        var oldPointId = DemoVectorStore.CreatePointId(oldGenerationId, chunkId);
        var newPointId = DemoVectorStore.CreatePointId(newGenerationId, chunkId);

        Assert.NotEqual(oldPointId, newPointId);
    }

    [Fact]
    public void CreatePointId_SeparatesChunksWithinSameGeneration()
    {
        var generationId = Guid.NewGuid();

        var firstPointId = DemoVectorStore.CreatePointId(generationId, Guid.NewGuid());
        var secondPointId = DemoVectorStore.CreatePointId(generationId, Guid.NewGuid());

        Assert.NotEqual(firstPointId, secondPointId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreatePointId_RejectsEmptyIdentifiers(bool emptyGeneration, bool emptyChunk)
    {
        var generationId = emptyGeneration ? Guid.Empty : Guid.NewGuid();
        var chunkId = emptyChunk ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => DemoVectorStore.CreatePointId(generationId, chunkId));
    }
}
