using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
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

            var delayBackoffType = (DelayBackoffType)Enum.Parse(typeof(DelayBackoffType), retryConfig.RetryType, true);
            var useJitter = retryConfig.FaultTolerancePolicy.JitterStrategy != null && retryConfig.FaultTolerancePolicy.JitterStrategy.Enabled;

            var faultTolerancePolicy = retryConfig.FaultTolerancePolicy;
            var failureThreshold = faultTolerancePolicy.FailureThreshold;
            var samplingDuration = TimeSpan.FromSeconds(faultTolerancePolicy.SamplingDurationSeconds);
            var breakDuration = TimeSpan.FromSeconds(faultTolerancePolicy.BreakDurationSeconds);
            var minThroughPut = 5;

            services.AddHttpClient(system)
                       .AddResilienceHandler(system, builder =>
                       {
                           Func<CircuitBreakerPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandle = async args =>
                           {
                               // Check for non-successful status code
                               if (!args.Outcome.Result.IsSuccessStatusCode)
                                   return true;

                               // Check for specific status codes
                               if (args.Outcome.Result.StatusCode == HttpStatusCode.ServiceUnavailable)
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


                           // See: https://www.pollydocs.org/strategies/retry.html
                           builder.AddRetry(new HttpRetryStrategyOptions
                           {
                               // Customize and configure the retry logic.
                               BackoffType = delayBackoffType,
                               MaxRetryAttempts = retryConfig.MaxRetries,
                               UseJitter = useJitter,
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
                           // builder.AddTimeout(TimeSpan.FromSeconds(5));
                       });
        }
    }
}
