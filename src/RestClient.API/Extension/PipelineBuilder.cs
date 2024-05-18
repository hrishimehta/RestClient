using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Simmy;
using Polly.Simmy.Fault;
using Polly.Simmy.Latency;
using Polly.Simmy.Outcomes;
using RestClient.Shared.Entities;
using System.Net;
using System.Reflection;

namespace RestClient.API.Extension
{
    /// <summary>
    /// Defines methods to build resilience pipelines for HTTP clients.
    /// </summary>
    public interface IPipelineBuilder
    {
        /// <summary>
        /// Builds a resilience pipeline for HTTP client requests based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicyName">Retry policy name</param>
        /// <returns>The resilience pipeline for HTTP client requests.</returns>
        ResiliencePipeline<HttpResponseMessage> BuildPipeline(string retryPolicyName);

        /// <summary>
        /// Gets the HTTP retry strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicy">The retry policy configuration.</param>
        /// <returns>The HTTP retry strategy options.</returns>
        HttpRetryStrategyOptions GetHttpRetryStrategyOptions(RetryPolicyConfiguration retryPolicy);

        /// <summary>
        /// Gets the HTTP circuit breaker strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicy">The retry policy configuration.</param>
        /// <returns>The HTTP circuit breaker strategy options.</returns>
        HttpCircuitBreakerStrategyOptions GetHttpCircuitBreakerStrategyOptions(RetryPolicyConfiguration retryPolicy);

        /// <summary>
        /// Gets the chaos fault strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicy">The retry policy configuration.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>The chaos fault strategy options.</returns>
        ChaosFaultStrategyOptions GetChaosFaultStrategyOptions(RetryPolicyConfiguration retryPolicy, ILogger logger);

        /// <summary>
        /// Gets the chaos outcome strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicy">The retry policy configuration.</param>
        /// <returns>The chaos outcome strategy options.</returns>
        ChaosOutcomeStrategyOptions<HttpResponseMessage> GetChaosOutcomeStrategyOptions(RetryPolicyConfiguration retryPolicy);

        /// <summary>
        /// Gets the chaos latency strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicy">The retry policy configuration.</param>
        /// <returns>The chaos latency strategy options.</returns>
        ChaosLatencyStrategyOptions GetChaosLatencyStrategyOptions(RetryPolicyConfiguration retryPolicy);
    }

    ///<inheritdoc/>
    internal class PipelineBuilder : IPipelineBuilder
    {
        private ILogger<PipelineBuilder> logger;
        private IConfiguration configuration;
        public PipelineBuilder(ILogger<PipelineBuilder> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        ///<inheritdoc/>
        public ResiliencePipeline<HttpResponseMessage> BuildPipeline(string retryPolicyName)
        {
            var retryPoliciesSection = this.configuration.GetSection("RetryPolicies").Get<List<RetryPolicyConfiguration>>();

            var retryPolicy = retryPoliciesSection.FirstOrDefault(policy => policy.Name == retryPolicyName);

            if (retryPolicy == null)
            {
                throw new ArgumentException($"Missing policy configuration {retryPolicyName}");
            }

            var resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>();

            resiliencePipeline.AddRetry(GetHttpRetryStrategyOptions(retryPolicy));

            resiliencePipeline.AddCircuitBreaker(GetHttpCircuitBreakerStrategyOptions(retryPolicy));

            resiliencePipeline.AddTimeout(TimeSpan.FromSeconds(retryPolicy.Timeout));

            resiliencePipeline.AddChaosFault(GetChaosFaultStrategyOptions(retryPolicy, logger));

            resiliencePipeline.AddChaosOutcome(GetChaosOutcomeStrategyOptions(retryPolicy));

            resiliencePipeline.AddChaosLatency(GetChaosLatencyStrategyOptions(retryPolicy));

            return resiliencePipeline.Build();
        }

        ///<inheritdoc/>
        public HttpRetryStrategyOptions GetHttpRetryStrategyOptions(RetryPolicyConfiguration retryPolicy)
        {
            Func<RetryPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandleForRetry = args =>
            {
                if (args.Outcome.Result == null && args.Outcome.Exception == null)
                {
                    return new ValueTask<bool>(false);
                }

                if (args.Outcome.Result != null && args.Outcome.Result.IsSuccessStatusCode)
                {
                    return new ValueTask<bool>(false);
                }

                var isRetrySettingsProvided = retryPolicy?.Retry != null && retryPolicy.Retry.RetryForHttpCodes != null && retryPolicy.Retry.RetryForExceptions != null;

                // if retry setting nor provided and response is not success status code than execute retry polciy
                if (!isRetrySettingsProvided && !args.Outcome.Result.IsSuccessStatusCode)
                    return new ValueTask<bool>(true);

                if (retryPolicy?.Retry.RetryForHttpCodes != null
                            && args.Outcome.Result != null
                            && retryPolicy.Retry.RetryForHttpCodes.Contains((int)args.Outcome.Result.StatusCode))
                    return new ValueTask<bool>(true);

                // Check for HttpRequestException
                if (retryPolicy?.Retry.RetryForExceptions != null
                        && args.Outcome.Exception != null
                         && args.Outcome.Exception != null && retryPolicy.Retry.RetryForExceptions.Contains(args.Outcome.Exception.GetType().FullName ?? string.Empty))
                    return new ValueTask<bool>(true);

                // Default: do not handle
                return new ValueTask<bool>(false);
            };
            var delayBackoffType = Enum.Parse<DelayBackoffType>(retryPolicy?.Retry.RetryType ?? DelayBackoffType.Constant.ToString(), true);

            return new HttpRetryStrategyOptions
            {
                // Customize and configure the retry logic.
                BackoffType = delayBackoffType,
                MaxRetryAttempts = retryPolicy?.Retry.MaxRetries ?? 0,
                UseJitter = retryPolicy?.Retry.UseJitter ?? false,
                ShouldHandle = shouldHandleForRetry,
                OnRetry = args =>
                {
                    logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Retry #{args.AttemptNumber}, Duration of attempt {args.Duration.TotalSeconds} seconds. Exception: {args.Outcome.Exception?.Message} Status: {args.Outcome.Result?.StatusCode}");
                    return new ValueTask();
                }
            };
        }

        ///<inheritdoc/>
        public HttpCircuitBreakerStrategyOptions GetHttpCircuitBreakerStrategyOptions(RetryPolicyConfiguration retryPolicy)
        {
            var faultTolerancePolicy = retryPolicy?.FaultTolerancePolicy;
            var failureThreshold = faultTolerancePolicy?.FailureThreshold ?? 0;
            var samplingDuration = TimeSpan.FromSeconds(faultTolerancePolicy?.SamplingDurationSeconds ?? 0);
            var breakDurationSeconds = TimeSpan.FromSeconds(faultTolerancePolicy?.BreakDurationSeconds ?? 0);
            var minThroughPut = faultTolerancePolicy?.MinThroughPut ?? 0;

            Func<CircuitBreakerPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandle = args =>
            {
                if (args.Outcome.Result == null && args.Outcome.Exception == null)
                {
                    return new ValueTask<bool>(false);
                }

                if (args.Outcome.Result != null && args.Outcome.Result.IsSuccessStatusCode)
                {
                    return new ValueTask<bool>(false);
                }

                var isCircuitBreakerSettingsProvided = faultTolerancePolicy != null 
                                                        && faultTolerancePolicy.OpenCircuitForHttpCodes != null
                                                        && faultTolerancePolicy.OpenCircuitForExceptions != null;

                // Check for non-successful status code
                if (!isCircuitBreakerSettingsProvided && !args.Outcome.Result.IsSuccessStatusCode)
                    return new ValueTask<bool>(true);

                if (faultTolerancePolicy?.OpenCircuitForHttpCodes != null
                            && args.Outcome.Result!=null
                            && faultTolerancePolicy.OpenCircuitForHttpCodes.Contains((int)args.Outcome.Result.StatusCode))
                    return new ValueTask<bool>(true);

                // Check for HttpRequestException
                if (faultTolerancePolicy?.OpenCircuitForExceptions != null
                         && args.Outcome.Exception != null
                         && faultTolerancePolicy.OpenCircuitForExceptions.Contains(args.Outcome.Exception.GetType().FullName ?? string.Empty))
                    return new ValueTask<bool>(true);

                // Default: do not handle
                return new ValueTask<bool>(false);
            };

            return new HttpCircuitBreakerStrategyOptions
            {
                // Customize and configure the circuit breaker logic.
                SamplingDuration = samplingDuration,
                BreakDuration = breakDurationSeconds,
                FailureRatio = failureThreshold,
                MinimumThroughput = minThroughPut,
                ShouldHandle = shouldHandle,
                Name = retryPolicy.Name,
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
            };
        }

        ///<inheritdoc/>
        public ChaosFaultStrategyOptions GetChaosFaultStrategyOptions(RetryPolicyConfiguration retryPolicy, ILogger logger)
        {
            if (retryPolicy.FaultChaos == null)
            {
                return new ChaosFaultStrategyOptions()
                {
                    InjectionRate = 0
                };
            }
            var exception = retryPolicy.FaultChaos.GetException();

            return new ChaosFaultStrategyOptions()
            {
                InjectionRate = retryPolicy.FaultChaos.InjectionRate,
                FaultGenerator = new FaultGenerator()
                            .AddException(() => exception),
                OnFaultInjected = args =>
                {
                    logger.LogInformation($"OnFaultInjected, Exception: {args.Fault.Message}, Operation: {args.Context.OperationKey}.");
                    return default;
                }
            };
        }

        ///<inheritdoc/>
        public ChaosOutcomeStrategyOptions<HttpResponseMessage> GetChaosOutcomeStrategyOptions(RetryPolicyConfiguration retryPolicy)
        {
            if (retryPolicy.LatencyChaos == null)
            {
                return new ChaosOutcomeStrategyOptions<HttpResponseMessage>
                {
                    InjectionRate = 0,
                };
            }


            if (!Enum.IsDefined(typeof(HttpStatusCode), retryPolicy.OutcomeChaos.StatusCode))
            {
                throw new ArgumentException($"Invalid outcome (status code) provided. Policy Name: {retryPolicy.Name}");
            }

            HttpStatusCode httpStatusCode = (HttpStatusCode)retryPolicy.OutcomeChaos.StatusCode;

            return new ChaosOutcomeStrategyOptions<HttpResponseMessage>()
            {
                InjectionRate = retryPolicy.OutcomeChaos.InjectionRate,
                OutcomeGenerator = new OutcomeGenerator<HttpResponseMessage>()
                                        .AddResult(() => new HttpResponseMessage(httpStatusCode)),
                OnOutcomeInjected = args =>
                {
                    logger.LogInformation($"OnOutcomeInjected , Outcome: {args.Outcome.Result}, Operation: {args.Context.OperationKey}.");
                    return default;
                }
            };
        }

        ///<inheritdoc/>
        public ChaosLatencyStrategyOptions GetChaosLatencyStrategyOptions(RetryPolicyConfiguration retryPolicy)
        {
            if (retryPolicy.LatencyChaos == null)
            {
                return new ChaosLatencyStrategyOptions
                {
                    InjectionRate = 0,
                };
            }

            if (retryPolicy.LatencyChaos.LatencySeconds < 0)
            {
                throw new ArgumentException($"Invalid Latency seconds provided Policy Name {retryPolicy.Name}");
            }

            return new ChaosLatencyStrategyOptions
            {
                InjectionRate = retryPolicy.LatencyChaos.InjectionRate,
                Latency = TimeSpan.FromSeconds(retryPolicy.LatencyChaos.LatencySeconds),
                OnLatencyInjected = args =>
                {
                    logger.LogInformation($"OnLatencyInjected, Latency: {args.Latency}, Operation: {args.Context.OperationKey}.");
                    return default;
                }
            };
        }
    }
}

