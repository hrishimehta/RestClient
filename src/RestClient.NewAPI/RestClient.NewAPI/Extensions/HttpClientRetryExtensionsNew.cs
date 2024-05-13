using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Simmy;
using Polly.Simmy.Behavior;
using Polly.Simmy.Fault;
using Polly.Simmy.Latency;
using RestClient.Shared.Entities;
using System.Net;

namespace RestClient.API.Extension
{
    public static class HttpClientRetryExtensionsNew
    {
        public static void AddHttpClientWithRetryPolicy2(this IServiceCollection services, string system, ILogger logger)
        {
            var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var systemConfig = configuration.GetSection(system).Get<SystemRetryConfiguration>();
            RetryPolicyConfiguration retryConfig = systemConfig?.RetryPolicy;
            // If no retry configuration is found, use the default retry policy
            if (retryConfig == null)
            {
                retryConfig = configuration.GetSection("DefaultRetryPolicy").Get<RetryPolicyConfiguration>();
            }


            var delayBackoffType = (DelayBackoffType)Enum.Parse(typeof(DelayBackoffType), retryConfig.Retry.RetryType, true);
            var useJitter = retryConfig.Retry.UseJitter;

            var faultTolerancePolicy = retryConfig.FaultTolerancePolicy;
            var failureThreshold = faultTolerancePolicy.FailureThreshold;
            var samplingDuration = TimeSpan.FromSeconds(faultTolerancePolicy.SamplingDurationSeconds);
            var breakDuration = TimeSpan.FromSeconds(faultTolerancePolicy.BreakDurationSeconds);
            var minThroughPut = faultTolerancePolicy.MinThroughPut;

            services.AddHttpClient(system)
                       .AddResilienceHandler(system, builder =>
                       {
                           Func<CircuitBreakerPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandle = async args =>
                           {
                               var isCircuitBreakerSettingsProvided = faultTolerancePolicy != null && faultTolerancePolicy.OpenCircuitForHttpCodes != null && faultTolerancePolicy.OpenCircuitForExceptions != null;

                               // Check for non-successful status code
                               if (!isCircuitBreakerSettingsProvided && !args.Outcome.Result.IsSuccessStatusCode)
                                   return true;

                               if (faultTolerancePolicy.OpenCircuitForHttpCodes != null
                                           && faultTolerancePolicy.OpenCircuitForHttpCodes.Contains((int)args.Outcome.Result.StatusCode))
                                   return true;

                               // Check for HttpRequestException
                               if (faultTolerancePolicy.OpenCircuitForExceptions != null
                                        && faultTolerancePolicy.OpenCircuitForExceptions.Contains(args.Outcome.Exception.GetType().FullName))
                                   return true;

                               // Default: do not handle
                               return false;
                           };

                           Func<RetryPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandleForRetry = async args =>
                           {
                               var isRetrySettingsProvided = retryConfig.Retry != null && retryConfig.Retry.RetryForHttpCodes != null && retryConfig.Retry.RetryForExceptions != null;

                               // if retry setting nor provided and response is not success status code than execute retry polciy
                               if (!isRetrySettingsProvided && !args.Outcome.Result.IsSuccessStatusCode)
                                   return true;

                               if (retryConfig.Retry.RetryForHttpCodes != null
                                           && retryConfig.Retry.RetryForHttpCodes.Contains((int)args.Outcome.Result.StatusCode))
                                   return true;

                               // Check for HttpRequestException
                               if (retryConfig.Retry.RetryForExceptions != null
                                        && retryConfig.Retry.RetryForExceptions.Contains(args.Outcome.Exception.GetType().FullName))
                                   return true;

                               // Default: do not handle
                               return false;
                           };


                           // See: https://www.pollydocs.org/strategies/retry.html
                           builder.AddRetry(new HttpRetryStrategyOptions
                           {
                               // Customize and configure the retry logic.
                               BackoffType = delayBackoffType,
                               MaxRetryAttempts = retryConfig.Retry.MaxRetries,
                               UseJitter = useJitter,
                               ShouldHandle = shouldHandleForRetry
                           });

                           // See: https://www.pollydocs.org/strategies/circuit-breaker.html
                           builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                           {
                               // Customize and configure the circuit breaker logic.
                               SamplingDuration = samplingDuration,
                               FailureRatio = failureThreshold,
                               MinimumThroughput = minThroughPut,
                               ShouldHandle = shouldHandle
                           });

                           // See: https://www.pollydocs.org/strategies/timeout.html
                           builder.AddTimeout(TimeSpan.FromSeconds(retryConfig.Timeout.TimeoutDuration));

                           //builder.AddChaosLatency(new ChaosLatencyStrategyOptions
                           //{

                           //    LatencyGenerator = static _ =>
                           //    {
                           //        var rnd = Random.Shared.NextDouble();
                           //        TimeSpan ts = rnd switch
                           //        {
                           //            < 0.4 => TimeSpan.FromMilliseconds(750),
                           //            >= 0.4 and < 0.8 => TimeSpan.FromSeconds(5),
                           //            _ => TimeSpan.Zero
                           //        };
                           //        return new ValueTask<TimeSpan>(ts);
                           //    }
                           //});
                       });
        }
    }
}
