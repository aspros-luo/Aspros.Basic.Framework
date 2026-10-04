using Polly;
using Polly.Timeout;
using System.Net;

namespace Aspros.Base.Framework.Infrastructure;

public static class PollyExtend
{
    public const string ClientName = "ExternalApiClient";

    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
        int maxRetryTimes = 3,
        int retryAttemptSeconds = 2,
        string policyKey = "RetryPolicy")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetryTimes);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryAttemptSeconds, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);

        return Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(IsTransientResponse)
            .WaitAndRetryAsync(
                maxRetryTimes,
                retryAttempt => TimeSpan.FromSeconds(
                    retryAttemptSeconds * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, delay, retryCount, _) =>
                {
                    var status = outcome.Result?.StatusCode.ToString()
                                 ?? outcome.Exception?.GetType().Name
                                 ?? "unknown";

                    Console.WriteLine(
                        $"HTTP retry {retryCount} after {delay.TotalSeconds:0.##}s; reason={status}");
                })
            .WithPolicyKey(policyKey);
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
        int failTimes = 3,
        int limitMin = 1,
        string policyKey = "CircuitBreakerPolicy")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failTimes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(limitMin, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);

        return Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(IsTransientResponse)
            .CircuitBreakerAsync(
                failTimes,
                TimeSpan.FromMinutes(limitMin),
                onBreak: (_, timespan) =>
                    Console.WriteLine(
                        $"HTTP circuit opened for {timespan.TotalSeconds:0}s."),
                onReset: () => Console.WriteLine("HTTP circuit reset."))
            .WithPolicyKey(policyKey);
    }

    public static IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy(
        string policyKey = "FallbackPolicy")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);

        return Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(IsTransientResponse)
            .FallbackAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent("Service temporarily unavailable.")
                },
                onFallbackAsync: (outcome, _) =>
                {
                    Console.WriteLine(
                        $"HTTP fallback triggered: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                    return Task.CompletedTask;
                })
            .WithPolicyKey(policyKey);
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(
        int timeoutSeconds = 5,
        string policyKey = "TimeoutPolicy")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutSeconds, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);

        return Policy
            .TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(timeoutSeconds),
                TimeoutStrategy.Optimistic)
            .WithPolicyKey(policyKey);
    }

    public static IAsyncPolicy<HttpResponseMessage> GetRateLimitPolicy(
        int requetTimes = 10,
        int minutes = 1,
        string policyKey = "RateLimitPolicy")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requetTimes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);

        return Policy
            .RateLimitAsync<HttpResponseMessage>(
                requetTimes,
                TimeSpan.FromMinutes(minutes))
            .WithPolicyKey(policyKey);
    }

    private static bool IsTransientResponse(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;

        return response.StatusCode == HttpStatusCode.RequestTimeout
               || response.StatusCode == (HttpStatusCode)429
               || code >= 500;
    }
}
