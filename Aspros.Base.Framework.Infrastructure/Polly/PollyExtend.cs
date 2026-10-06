using Polly;
using Polly.Timeout;
using System.Net;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Factory methods for common HTTP resilience policies.
/// 常用 HTTP 弹性策略工厂。
///
/// <para>
/// Retry and circuit-breaker policies only treat request failures, 408, 429 and
/// server-side 5xx responses as transient. Business 4xx errors are not retried.
/// Retry/CircuitBreaker 只把请求异常、408、429 和 5xx 视为临时性故障；
/// 普通业务 4xx 不会被无意义地重复请求。
/// </para>
/// </summary>
public static class PollyExtend
{
    public const string ClientName = "ExternalApiClient";

    /// <summary>
    /// Creates an exponential-backoff retry policy.
    /// 创建指数退避重试策略。
    /// </summary>
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

    /// <summary>
    /// Creates a circuit-breaker policy for repeated transient failures.
    /// 创建用于连续临时故障的熔断策略。
    /// </summary>
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

    /// <summary>
    /// Creates a fallback policy returning HTTP 503 for transient dependency failures.
    /// 创建在临时依赖故障时返回 HTTP 503 的降级策略。
    /// </summary>
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

    /// <summary>
    /// Creates an optimistic timeout policy.
    /// 创建 Optimistic 超时策略。
    /// </summary>
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

    /// <summary>
    /// Creates a local rate-limit policy.
    /// 创建本地限流策略。
    /// </summary>
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