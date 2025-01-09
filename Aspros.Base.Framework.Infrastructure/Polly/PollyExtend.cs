using Polly;
using Polly.Timeout;
using System.Net;

namespace Aspros.Base.Framework.Infrastructure
{
    public static class PollyExtend
    {
        public const string ClientName = "ExternalApiClient";

        /// <summary>
        /// 配置重试策略（最大重试 3 次，每次重试间隔 2 秒）
        /// </summary>
        /// <param name="maxRetryTimes"></param>
        /// <param name="retryAttemptSeconds"></param>
        /// <param name="policyKey"></param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int maxRetryTimes = 3, int retryAttemptSeconds = 2, string policyKey = "RetryPolicy")
        {
            return Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    maxRetryTimes,  // 最大重试次数
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(retryAttemptSeconds, retryAttempt)),  // 指数回退：每次重试的时间间隔
                    onRetry: (outcome, timeSpan, retryCount, context) =>
                    {
                        // 重试回调
                        Console.WriteLine($"重试 {retryCount} 在 {timeSpan.TotalSeconds} 秒后， 结果 {outcome.Result.StatusCode}");
                    })
                .WithPolicyKey(policyKey);  // 为策略指定一个键
        }

        /// <summary>
        /// 配置熔断策略（最大失败 3 次，1 分钟熔断）
        /// </summary>
        /// <param name="failTimes"></param>
        /// <param name="limitMin"></param>
        /// <param name="policyKey"></param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(int failTimes = 3, int limitMin = 1, string policyKey = "CircuitBreakerPolicy")
        {
            return Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                         .CircuitBreakerAsync(failTimes, TimeSpan.FromMinutes(limitMin),
                             onBreak: (outcome, timespan) =>
                             {
                                 // Log circuit break
                                 Console.WriteLine($"请求熔断! 我们将在 {timespan.TotalSeconds} 后重试.");
                             },
                             onReset: () => Console.WriteLine("Circuit reset!"))
                         .WithPolicyKey(policyKey);  // 为策略指定一个键
        }

        /// <summary>
        /// 降级/回退策略
        /// </summary>
        /// <param name="policyKey">策略key=FallbackPolicy</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy(string policyKey = "FallbackPolicy")
        {
            return Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                         .FallbackAsync(
                            new HttpResponseMessage()  // 默认的备用响应
                            {
                                StatusCode = HttpStatusCode.OK,
                                Content = new StringContent("返回降级信息")
                            },
                            onFallbackAsync: (exception, context) =>
                            {
                                // 失败回调
                                Console.WriteLine($"Request failed, falling back. Exception: {exception}");
                                return Task.CompletedTask;  // 你可以做一些异步操作
                            })
                         .WithPolicyKey(policyKey);  // 为策略指定一个键

        }

        /// <summary>
        /// 超时策略
        /// </summary>
        /// <param name="timeoutSeconds">超时时间=5</param>
        /// <param name="policyKey">策略key=TimeoutPolicy</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds = 5, string policyKey = "TimeoutPolicy")
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeoutSeconds), TimeoutStrategy.Optimistic)  // 设置超时时间
                         .WithPolicyKey(policyKey);  // 为策略指定一个键
        }

        /// <summary>
        /// 限流策略
        /// </summary>
        /// <param name="requetTimes">最多允许 10 次请求</param>
        /// <param name="minutes">每1分钟</param>
        /// <param name="policyKey">策略key=RateLimitPolicy</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> GetRateLimitPolicy(int requetTimes = 10, int minutes = 1, string policyKey = "RateLimitPolicy")
        {
            return Policy.RateLimitAsync<HttpResponseMessage>(requetTimes, TimeSpan.FromMinutes(minutes))  // 每分钟最多允许 10 次请求
                         .WithPolicyKey(policyKey);  // 为策略指定一个键
        }
    }
}
