using Aspros.Base.Framework.Algorithms;
using Aspros.Base.Framework.ClickHouse;
using Aspros.Base.Framework.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// This application consumes every published Framework package through NuGet,
// never through a ProjectReference to the repository source.
builder.Services.AddAsprosFramework();

var rankingProbe = RankingAlgorithms.ReciprocalRankFusion(
    new[] { new RankingSource<string>("package-consumer", new[] { "framework-package-ok" }) },
    candidate => candidate);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "framework-core-api",
    status = "ok",
    framework = "Aspros.Basic.Framework",
    targetFramework = "net10.0"
}));

app.MapGet("/framework", (IWorkContext workContext) => Results.Ok(new
{
    service = "framework-core-api",
    canResolveWorkContext = workContext is not null,
    algorithmRank = rankingProbe.Single().Rank,
    clickHouseAssembly = typeof(ClickHouseServiceCollectionExtensions).Assembly.GetName().Name
}));

app.Run();

public partial class Program { }
