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
                           builder.AddRetry(pipelineBuilder.GetRetryStrategyOptions<HttpResponseMessage>(retryPolicy));

                           // See: https://www.pollydocs.org/strategies/circuit-breaker.html
                           builder.AddCircuitBreaker(pipelineBuilder.GetCircuitBreakerStrategyOptions<HttpResponseMessage>(retryPolicy));

                           // See: https://www.pollydocs.org/strategies/timeout.html
                           builder.AddTimeout(TimeSpan.FromSeconds(retryPolicy.Timeout));

                           if (isChaosEnabled)
                           {
                               ApplyChaosPoliciesInOrder(pipelineBuilder, builder, retryPolicy);
                           }
                       });
                }
            }
        }

        public static void ApplyChaosPoliciesInOrder(IPipelineBuilder pipelineBuilder, ResiliencePipelineBuilder<HttpResponseMessage> builder, RetryPolicyConfiguration retryPolicy)
        {
            foreach (var setting in retryPolicy.ChaosPolicies)
            {
                switch (setting.Type)
                {
                    case "Fault":
                        builder.AddChaosFault(pipelineBuilder.GetHttpChaosFaultStrategyOptions(setting));
                        break;
                    case "Latency":
                        builder.AddChaosLatency(pipelineBuilder.GetChaosLatencyStrategyOptions(setting));
                        break;
                    case "Outcome":
                        builder.AddChaosOutcome(pipelineBuilder.GetChaosOutcomeStrategyOptions(setting));
                        break;
                }
            }
        }
    }
}
