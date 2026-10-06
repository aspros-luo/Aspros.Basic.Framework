using Aspros.Base.Framework.Infrastructure;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class FrameworkPollyRegressionTests
{
    [Fact]
    public async Task TimeoutPolicy_StopsAnOverdueOperation()
    {
        var policy = PollyExtend.GetTimeoutPolicy(timeoutSeconds: 1);

        await Assert.ThrowsAsync<TimeoutRejectedException>(
            () => policy.ExecuteAsync(
                async cancellationToken =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task CircuitBreakerPolicy_StopsCallingAfterConfiguredFailures()
    {
        var policy = PollyExtend.GetCircuitBreakerPolicy(
            failTimes: 2,
            limitMin: 1);

        for (var i = 0; i < 2; i++)
        {
            using var response = await policy.ExecuteAsync(
                () => Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => policy.ExecuteAsync(
                () => Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK))));
    }

    [Fact]
    public async Task FallbackPolicy_Returns503ForTransientException()
    {
        var policy = PollyExtend.GetFallbackPolicy();

        using var response = await policy.ExecuteAsync(
            () => Task.FromException<HttpResponseMessage>(
                new HttpRequestException("regression")));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
