namespace Aspros.Base.Framework.RAG.Models;

public enum DocumentStatus
{
    Active = 1,
    Deleted = 2
}

public enum DocumentVersionStatus
{
    Ready = 1,
    Superseded = 2
}

public enum IndexGenerationStatus
{
    Building = 1,
    Active = 2,
    Retired = 3,
    Failed = 4
}

public enum IngestionJobStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4
}

public enum IngestionJobType
{
    IngestDocument = 1,
    RebuildIndex = 2,
    DeleteIndex = 3
}
