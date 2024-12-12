using AspectCore.DynamicProxy;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Aspros.Base.Framework.Infrastructure
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HystrixCommandAttribute : AbstractInterceptorAttribute
    {
        /// <summary>        
        /// 最多重试几次，如果为0则不重试       
        /// </summary>       
        public int MaxRetryTimes { get; set; } = 0;
        /// <summary>        
        ///  重试间隔的毫秒数       
        ///  </summary>
        public int RetryIntervalMilliseconds { get; set; } = 100;
        /// <summary>       
        /// 是否启用熔断      
        ///  </summary>     
        public bool IsEnableCircuitBreaker { get; set; } = false;
        /// <summary>       
        /// 熔断前出现允许错误几次       
        ///  </summary>    
        public int ExceptionsAllowedBeforeBreaking { get; set; } = 3;
        /// <summary>        
        /// 熔断多长时间（毫秒）        
        /// </summary>        

        public int MillisecondsOfBreak { get; set; } = 1000;
        /// <summary>        
        ///  执行超过多少毫秒则认为超时（0表示不检测超时）      
        ///   </summary>      

        public int TimeOutMilliseconds { get; set; } = 0;
        /// <summary>        
        /// /// 缓存多少毫秒（0表示不缓存），用“类名+方法名+所有参数ToString拼接”做缓存Key        
        /// /// </summary>        
        public int CacheTTLMilliseconds { get; set; } = 0;
        private static readonly ConcurrentDictionary<MethodInfo, ResiliencePipeline> policies = new ConcurrentDictionary<MethodInfo, ResiliencePipeline>();
        private static readonly IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
        /// <summary>        
        ///  HystrixCommandAttribute        
        ///  </summary>       
        ///  <param name="fallBackMethod">降级的方法名</param>   
        public HystrixCommandAttribute(string fallBackMethod)
        {
            this.FallBackMethod = fallBackMethod;
        }
        public string FallBackMethod { get; set; }
        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            //一个HystrixCommand中保持一个policy对象即可           
            //其实主要是CircuitBreaker要求对于同一段代码要共享一个policy对象        
            //根据反射原理，同一个方法的MethodInfo是同一个对象，但是对象上取出来的HystrixCommandAttribute     
            //每次获取的都是不同的对象，因此以MethodInfo为Key保存到policies中，确保一个方法对应一个policy实例
            policies.TryGetValue(context.ServiceMethod, out ResiliencePipeline policy);
            lock (policies)
            //因为Invoke可能是并发调用，因此要确保policies赋值的线程安全
            {
                if (policy == null)
                {
                    // Create and use the ResiliencePipeline.
                    //
                    // Use the ResiliencePipelineBuilder to start building the resilience pipeline
                    var pipeline = new ResiliencePipelineBuilder();
                        
                        //.Build(); // After all necessary strategies are added, call Build() to create the pipeline.
                    if (IsEnableCircuitBreaker)
                    {
                        pipeline.AddCircuitBreaker(new CircuitBreakerStrategyOptions {
                            
                        });
                        //policy = policy.WrapAsync(Policy.Handle<Exception>().CircuitBreakerAsync(ExceptionsAllowedBeforeBreaking, TimeSpan.FromMilliseconds(MillisecondsOfBreak)));
                    }
                    if (TimeOutMilliseconds > 0)
                    {
                        pipeline.AddTimeout(new TimeoutStrategyOptions() { Timeout= TimeSpan.FromMilliseconds(TimeOutMilliseconds) });
                        //policy = policy.WrapAsync(Policy.TimeoutAsync(() => TimeSpan.FromMilliseconds(TimeOutMilliseconds), Polly.Timeout.TimeoutStrategy.Pessimistic));
                    }
                    if (MaxRetryTimes > 0)
                    {
                        pipeline.AddRetry(new RetryStrategyOptions
                         {
                             ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                             Delay = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds),
                             MaxRetryAttempts = MaxRetryTimes,
                             BackoffType = DelayBackoffType.Constant
                         });
                        //policy = policy.WrapAsync(Policy.Handle<Exception>().WaitAndRetryAsync(MaxRetryTimes, i => TimeSpan.FromMilliseconds(RetryIntervalMilliseconds)));
                    }

                    var fallback = new ResiliencePipelineBuilder<Exception>()
                        .AddFallback(new Polly.Fallback.FallbackStrategyOptions<Exception>()
                        {
                            FallbackAction = args =>
                            {
                                ResiliencePropertyKey<AspectContext> propertyKey = new("aspectContext");
                                var aspectContext = args.Context.Properties.GetValue(propertyKey, context);
                                var fallBackMethod = context.ServiceMethod.DeclaringType.GetMethod(this.FallBackMethod);
                                Object fallBackResult = fallBackMethod.Invoke(context.Implementation, context.Parameters);
                                //不能如下这样，因为这是闭包相关，如果这样写第二次调用Invoke的时候context指向的                 
                                //还是第一次的对象，所以要通过Polly的上下文来传递AspectContext                  
                                context.ReturnValue = fallBackResult;
                                aspectContext.ReturnValue = fallBackResult;
                                return Outcome.FromResultAsValueTask(new Exception());
                            }
                        }
                        ).Build();

                    //var policyFallBack = Policy.Handle<Exception>().FallbackAsync(async (ctx, t) =>
                    //{
                    //    AspectContext aspectContext = (AspectContext)ctx["aspectContext"];
                    //    var fallBackMethod = context.ServiceMethod.DeclaringType.GetMethod(this.FallBackMethod);
                    //    Object fallBackResult = fallBackMethod.Invoke(context.Implementation, aspectContext.Parameters);
                    //    //不能如下这样，因为这是闭包相关，如果这样写第二次调用Invoke的时候context指向的                 
                    //    //还是第一次的对象，所以要通过Polly的上下文来传递AspectContext                  
                    //    context.ReturnValue = fallBackResult;
                    //    aspectContext.ReturnValue = fallBackResult;
                    //}, async (ex, t) => { });
                    //policy = policyFallBack.WrapAsync(policy);


                    //放入
                    policy = pipeline.Build();
                    
                    policies.TryAdd(context.ServiceMethod, policy);
                }
            }
            //把本地调用的AspectContext传递给Polly，主要给FallbackAsync中使用，避免闭包的坑
            //Context pollyCtx = new Context();

            ResilienceContext pollyCtx = ResilienceContextPool.Shared.Get();

            //pollyCtx["aspectContext"] = context;
            ResiliencePropertyKey<AspectContext> propertyKey1 = new("aspectContext");
            pollyCtx.Properties.Set(propertyKey1, context);
            //Install-Package Microsoft.Extensions.Caching.Memory
            if (CacheTTLMilliseconds > 0)
            {
                //用类名+方法名+参数的下划线连接起来作为缓存key
                string cacheKey = "HystrixMethodCacheManager_Key_" + context.ServiceMethod.DeclaringType
                    + "." + context.ServiceMethod + string.Join("_", context.Parameters);
                //尝试去缓存中获取。如果找到了，则直接用缓存中的值做返回值
                if (memoryCache.TryGetValue(cacheKey, out var cacheValue))
                {
                    context.ReturnValue = cacheValue;
                }
                else
                {
                    //如果缓存中没有，则执行实际被拦截的方法
                    await policy.ExecuteAsync(async ctx => await next(context), pollyCtx);
                    //存入缓存中
                    using (var cacheEntry = memoryCache.CreateEntry(cacheKey))
                    {
                        cacheEntry.Value = context.ReturnValue;
                        cacheEntry.AbsoluteExpiration = DateTime.Now + TimeSpan.FromMilliseconds(CacheTTLMilliseconds);
                    }
                }
            }
            else
            //如果没有启用缓存，就直接执行业务方法
            {
                await policy.ExecuteAsync(async ctx => await next(context), pollyCtx);
            }
        }
    }
}

