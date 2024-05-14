using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using RestClient.Shared.Entities;

namespace RestClient.API.Extension
{
    public static class HttpClientRetryExtensions
    {
        public static void AddHttpClientWithRetryPolicy(this IServiceCollection services, string system, ILogger logger)
        {
            var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var systemConfig = configuration.GetSection(system).Get<SystemRetryConfiguration>();
            RetryPolicyConfiguration? retryConfig = systemConfig?.RetryPolicy;

            // If no retry configuration is found, use the default retry policy
            if (retryConfig == null)
            {
                retryConfig = configuration.GetSection("DefaultRetryPolicy").Get<RetryPolicyConfiguration>();
            }


            var delayBackoffType = Enum.Parse<DelayBackoffType>(retryConfig?.Retry.RetryType ?? DelayBackoffType.Constant.ToString(), true);
            var useJitter = retryConfig?.Retry.UseJitter ?? false;

            var faultTolerancePolicy = retryConfig?.FaultTolerancePolicy;
            var failureThreshold = faultTolerancePolicy?.FailureThreshold ?? 0;
            var samplingDuration = TimeSpan.FromSeconds(faultTolerancePolicy?.SamplingDurationSeconds ?? 0);
            var breakDurationSeconds = TimeSpan.FromSeconds(faultTolerancePolicy?.BreakDurationSeconds ?? 0);
            var minThroughPut = faultTolerancePolicy?.MinThroughPut ?? 0;

            services.AddHttpClient(system)
                       .AddResilienceHandler(system, builder =>
                       {
                           Func<CircuitBreakerPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandle = args =>
                           {
                               if (args.Outcome.Result == null)
                               {
                                   return new ValueTask<bool>(false);
                               }

                               var isCircuitBreakerSettingsProvided = faultTolerancePolicy != null && faultTolerancePolicy.OpenCircuitForHttpCodes != null && faultTolerancePolicy.OpenCircuitForExceptions != null;

                               // Check for non-successful status code
                               if (!isCircuitBreakerSettingsProvided && !args.Outcome.Result.IsSuccessStatusCode)
                                   return new ValueTask<bool>(true);

                               if (faultTolerancePolicy?.OpenCircuitForHttpCodes != null
                                           && faultTolerancePolicy.OpenCircuitForHttpCodes.Contains((int)args.Outcome.Result.StatusCode))
                                   return new ValueTask<bool>(true);

                               // Check for HttpRequestException
                               if (faultTolerancePolicy?.OpenCircuitForExceptions != null
                                        && args.Outcome.Exception != null && faultTolerancePolicy.OpenCircuitForExceptions.Contains(args.Outcome.Exception.GetType().FullName ?? string.Empty))
                                   return new ValueTask<bool>(true);

                               // Default: do not handle
                               return new ValueTask<bool>(false);
                           };

                           Func<RetryPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandleForRetry = args =>
                           {
                               if (args.Outcome.Result == null)
                               {
                                   return new ValueTask<bool>(false);
                               }

                               var isRetrySettingsProvided = retryConfig?.Retry != null && retryConfig.Retry.RetryForHttpCodes != null && retryConfig.Retry.RetryForExceptions != null;

                               // if retry setting nor provided and response is not success status code than execute retry polciy
                               if (!isRetrySettingsProvided && !args.Outcome.Result.IsSuccessStatusCode)
                                   return new ValueTask<bool>(true);

                               if (retryConfig?.Retry.RetryForHttpCodes != null
                                           && retryConfig.Retry.RetryForHttpCodes.Contains((int)args.Outcome.Result.StatusCode))
                                   return new ValueTask<bool>(true);

                               // Check for HttpRequestException
                               if (retryConfig?.Retry.RetryForExceptions != null
                                        && args.Outcome.Exception != null && retryConfig.Retry.RetryForExceptions.Contains(args.Outcome.Exception.GetType().FullName ?? string.Empty))
                                   return new ValueTask<bool>(true);

                               // Default: do not handle
                               return new ValueTask<bool>(false);
                           };


                           // See: https://www.pollydocs.org/strategies/retry.html
                           builder.AddRetry(new HttpRetryStrategyOptions
                           {
                               // Customize and configure the retry logic.
                               BackoffType = delayBackoffType,
                               MaxRetryAttempts = retryConfig?.Retry.MaxRetries ?? 0,
                               UseJitter = useJitter,
                               ShouldHandle = shouldHandleForRetry,
                               OnRetry = args =>
                               {

                                   logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Retry #{args.AttemptNumber}, Duration of attempt {args.Duration.TotalSeconds} seconds. Exception: {args.Outcome.Exception?.Message} Status: {args.Outcome.Result?.StatusCode}");
                                   return new ValueTask();
                               }
                           });

                           // See: https://www.pollydocs.org/strategies/circuit-breaker.html
                           builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                           {
                               // Customize and configure the circuit breaker logic.
                               SamplingDuration = samplingDuration,
                               BreakDuration = breakDurationSeconds,                               
                               FailureRatio = failureThreshold,
                               MinimumThroughput = minThroughPut,
                               ShouldHandle = shouldHandle,
                               OnOpened = args =>
                               {
                                   logger.LogInformation($"Circuit Opened. Break duration {args.BreakDuration}, IsManual {args.IsManual},  Exception: {args.Outcome.Exception?.Message} Status: {args.Outcome.Result?.StatusCode} ");
                                   return new ValueTask();
                               },
                               OnClosed = args =>
                               {
                                   logger.LogInformation($"Circuit Breaker OnClosed.IsManual {args.IsManual},  Exception: {args.Outcome.Exception?.Message} Status: {args.Outcome.Result?.StatusCode} ");
                                   return new ValueTask();
                               },
                               OnHalfOpened = args =>
                               {
                                   logger.LogInformation($"Circuit Breaker OnHalfOpened. Operation key {args.Context.OperationKey}");
                                   return new ValueTask();
                               },
                           });

                           // See: https://www.pollydocs.org/strategies/timeout.html
                           builder.AddTimeout(TimeSpan.FromSeconds(retryConfig?.Timeout.TimeoutDuration ?? 0));

                       });
        }

    }
}
