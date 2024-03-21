using Polly;
using Polly.Extensions.Http;
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
            RetryPolicyConfiguration retryConfig = systemConfig?.RetryPolicy;
            // If no retry configuration is found, use the default retry policy
            if (retryConfig == null)
            {
                retryConfig = configuration.GetSection("DefaultRetryPolicy").Get<RetryPolicyConfiguration>();
            }


            services.AddHttpClient(system, client =>
            {
                client.BaseAddress = new Uri(systemConfig.BaseUrl);
            })
            .AddPolicyHandler(retryConfig.RetryType == "Exponential"
                ? GetExponentialRetryPolicy(retryConfig.MaxRetries, retryConfig.BackoffExponentialBase ?? 2, retryConfig.FaultTolerancePolicy, logger)
                : GetConstantRetryPolicy(retryConfig.MaxRetries, retryConfig.RetryInterval, retryConfig.FaultTolerancePolicy, logger));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetExponentialRetryPolicy(int maxRetries, int exponentialBase, FaultTolerancePolicy faultTolerancePolicy, ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(maxRetries, retryCount => CalculateRetryDelay(exponentialBase, retryCount, faultTolerancePolicy),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Exponential Retry #{retryCount} after {timeSpan.TotalSeconds} seconds. Exception: {exception?.Exception?.Message}");
                    })
                .WrapAsync(ApplyCircuitBreaker(faultTolerancePolicy, logger));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetConstantRetryPolicy(int maxRetries, int retryInterval, FaultTolerancePolicy faultTolerancePolicy, ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(maxRetries, retryCount => TimeSpan.FromSeconds(retryInterval),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Constant Retry #{retryCount} after {timeSpan.TotalSeconds} seconds. Exception: {exception?.Exception?.Message}");
                    })
                .WrapAsync(ApplyCircuitBreaker(faultTolerancePolicy, logger));
        }



        private static IAsyncPolicy<HttpResponseMessage> ApplyCircuitBreaker(FaultTolerancePolicy faultTolerancePolicy, ILogger logger)
        {
            if (faultTolerancePolicy == null || !faultTolerancePolicy.Enabled)
            {
                // If faultTolerancePolicy is not provided or explicitly disabled, return a NoOp policy
                return Policy.NoOpAsync<HttpResponseMessage>();
            }

            var failureThreshold = faultTolerancePolicy.FailureThreshold;
            var samplingDuration = TimeSpan.FromSeconds(faultTolerancePolicy.SamplingDurationSeconds);
            var breakDuration = TimeSpan.FromSeconds(faultTolerancePolicy.BreakDurationSeconds);
            var jitterStrategy = faultTolerancePolicy.JitterStrategy;

            var circuitBreaker = Policy.Handle<HttpRequestException>()
                     .OrResult<HttpResponseMessage>(response => faultTolerancePolicy.OpenCircuitForHttpCodes?.Contains((int)response.StatusCode) ?? false)
                     .Or<Exception>(ex => faultTolerancePolicy.OpenCircuitForExceptions?.Contains(ex.GetType().FullName) ?? false)
                     .AdvancedCircuitBreakerAsync(
                        failureThreshold,
                        TimeSpan.FromSeconds(faultTolerancePolicy.SamplingDurationSeconds),
                        5,
                        TimeSpan.FromSeconds(faultTolerancePolicy.BreakDurationSeconds)
                     , onBreak: (result, duration) =>
                     {
                         // Your logic to handle the circuit being open
                         logger.LogWarning($"Circuit Breaker Opened for {duration.TotalSeconds} seconds. Exception: {result?.Exception?.Message}");
                     },
                     onReset: () =>
                     {
                         // Your logic when the circuit resets
                         logger.LogInformation("Circuit Breaker Reset");
                     },
                      onHalfOpen: () =>
                      {
                          // Your logic when the circuit transitions to half-open state
                          logger.LogInformation("Circuit Breaker Half-Open");
                      });

            return circuitBreaker;
        }


        private static TimeSpan CalculateRetryDelay(int exponentialBase, int retryAttempt, FaultTolerancePolicy faultTolerancePolicy)
        {
            var baseDelay = Math.Pow(exponentialBase, retryAttempt);
            var delayWithJitter = GetJitter(baseDelay, faultTolerancePolicy.JitterStrategy) + TimeSpan.FromSeconds(baseDelay);
            return delayWithJitter;
        }

        private static TimeSpan GetJitter(double baseValue, JitterStrategy jitterStrategy)
        {
            if (jitterStrategy.Enabled)
            {
                var random = new Random();

                // Calculate the maximum jitter based on the specified percentage of baseValue
                var jitter = (int)(jitterStrategy.Percentage / 100.0 * baseValue);

                // Custom jitter implementation using a random number between -jitter and jitter
                var randomNumber = random.Next(-jitter, jitter + 1);

                return TimeSpan.FromMilliseconds(randomNumber);
            }

            return TimeSpan.Zero;
        }

    }
}
