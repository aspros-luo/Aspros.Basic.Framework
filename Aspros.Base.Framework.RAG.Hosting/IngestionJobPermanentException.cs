namespace Aspros.Base.Framework.RAG.Hosting;

/// <summary>
/// Signals that retrying the current ingestion job cannot succeed without changing its input.
/// The worker marks the job as terminally failed.
/// </summary>
public sealed class IngestionJobPermanentException : Exception
{
    public IngestionJobPermanentException(string message)
        : base(message)
    {
    }

    public IngestionJobPermanentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
