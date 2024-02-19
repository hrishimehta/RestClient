using Polly;
using Polly.Extensions.Http;
using RestClient.Shared.Entities;
using System;

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
                ? GetExponentialRetryPolicy(retryConfig.MaxRetries, retryConfig.BackoffExponentialBase ?? 2, logger)
                : GetConstantRetryPolicy(retryConfig.MaxRetries, retryConfig.RetryInterval, logger));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetExponentialRetryPolicy(int maxRetries, int exponentialBase, ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(maxRetries, retryCount => TimeSpan.FromSeconds(Math.Pow(exponentialBase, retryCount)),
                (exception, timeSpan, retryCount, context) =>
                    {
                        // Log each retry attempt
                        logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Exponential Retry #{retryCount} after {timeSpan.TotalSeconds} seconds. Exception: {exception?.Exception?.Message}");
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetConstantRetryPolicy(int maxRetries, int retryInterval, ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(maxRetries, retryCount => TimeSpan.FromSeconds(retryInterval),
                (exception, timeSpan, retryCount, context) =>
                {
                    // Log each retry attempt
                    logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Constant Retry #{retryCount} after {timeSpan.TotalSeconds} seconds. Exception: {exception?.Exception?.Message}");
                });
        }
    }
}
