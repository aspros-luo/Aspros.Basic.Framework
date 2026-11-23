using Aspros.Base.Framework.Domain;
using Aspros.Base.Framework.Domain.ValueObjects;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class DomainStatusRegressionTests
{
    [Fact]
    public void BasicEntity_DefaultStatus_UsesValueObjectsNamespace()
    {
        var entity = new BasicEntity();

        Assert.Equal(EntityStatus.Normal, entity.Status);
    }
}
