using Aspros.Base.Framework.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// This application consumes the framework exactly as an external microservice would:
// through a NuGet package instead of a ProjectReference.
builder.Services.AddAsprosFramework();

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
    canResolveWorkContext = workContext is not null
}));

app.Run();

public partial class Program { }
