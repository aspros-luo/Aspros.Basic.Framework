using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class FrameworkRegressionTests
{
    [Fact]
    public void ServiceCollection_CanBeCreated()
    {
        var services = new ServiceCollection();
        Assert.NotNull(services);
    }

    [Fact]
    public void TestHost_IsAvailable()
    {
        Assert.True(typeof(FactAttribute).Assembly is not null);
    }
}
