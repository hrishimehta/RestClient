using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Simmy;
using RestClient.Shared.Entities;

namespace RestClient.API.Extension
{
    public static class HttpClientRetryExtensions
    {
        public static void AddHttpClientWithRetryPolicy(this IServiceCollection services, string system, ILogger logger)
        {
            var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var pipelineBuilder = serviceProvider.GetRequiredService<IPipelineBuilder>();
            var systemConfig = configuration.GetSection(system).Get<SystemRetryConfiguration>();

            RetryPolicyConfiguration? retryConfig = systemConfig?.RetryPolicy;

            // If no retry configuration is found, use the default retry policy
            if (retryConfig == null)
            {
                services.AddHttpClient(system).AddStandardResilienceHandler();
                return;
            }

            services.AddHttpClient(system)
                       .AddResilienceHandler(system, builder =>
                       {
                           // See: https://www.pollydocs.org/strategies/retry.html
                           builder.AddRetry(pipelineBuilder.GetHttpRetryStrategyOptions(retryConfig));

                           // See: https://www.pollydocs.org/strategies/circuit-breaker.html
                           builder.AddCircuitBreaker(pipelineBuilder.GetHttpCircuitBreakerStrategyOptions(retryConfig));

                           // See: https://www.pollydocs.org/strategies/timeout.html
                           builder.AddTimeout(TimeSpan.FromSeconds(retryConfig.Timeout));

                       });
        }

        public static void AddHttpClientWithRetryPolicy(this IServiceCollection services, ILogger logger)
        {
            var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var pipelineBuilder = serviceProvider.GetRequiredService<IPipelineBuilder>();
            var httpClientSettings = configuration.GetSection("HttpClient").Get<List<HttpClientSettings>>();
            var retryPolicySettings = configuration.GetSection("RetryPolicySettings").Get<List<RetryPolicySettings>>();

            bool isChaosEnabled = configuration.GetValue("IsChaosEnabled", false);

            if (httpClientSettings == null)
            {
                throw new ArgumentException("Httpclient setitng is missing in appsettings file");
            }

            foreach (var httpClientSetting in httpClientSettings)
            {
                var retryPolicyName = httpClientSetting.RetryPolicyName;
                var retryPolicy = retryPolicySettings?.FirstOrDefault(r => r.Name == retryPolicyName)?.Policy;


                if (retryPolicy == null)
                {
                    services.AddHttpClient(httpClientSetting.Name).AddStandardResilienceHandler();
                    return;
                }
                else
                {
                    services.AddHttpClient(httpClientSetting.Name)
                       .AddResilienceHandler(httpClientSetting.Name, builder =>
                       {
                           // See: https://www.pollydocs.org/strategies/retry.html
                           builder.AddRetry(pipelineBuilder.GetHttpRetryStrategyOptions(retryPolicy));

                           // See: https://www.pollydocs.org/strategies/circuit-breaker.html
                           builder.AddCircuitBreaker(pipelineBuilder.GetHttpCircuitBreakerStrategyOptions(retryPolicy));

                           // See: https://www.pollydocs.org/strategies/timeout.html
                           builder.AddTimeout(TimeSpan.FromSeconds(retryPolicy.Timeout));

                           if (isChaosEnabled)
                           {
                               builder.AddChaosFault(pipelineBuilder.GetChaosFaultStrategyOptions(retryPolicy, logger));

                               builder.AddChaosOutcome(pipelineBuilder.GetChaosOutcomeStrategyOptions(retryPolicy));

                               builder.AddChaosLatency(pipelineBuilder.GetChaosLatencyStrategyOptions(retryPolicy));
                           }
                       });
                }
            }
        }

    }
}
