namespace Aspros.Base.Framework.Infrastructure;

public sealed record ServiceEndpoint(
    Uri Address,
    IReadOnlyDictionary<string, string> Metadata);
