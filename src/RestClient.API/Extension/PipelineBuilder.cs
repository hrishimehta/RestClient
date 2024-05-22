using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Simmy.Fault;
using Polly.Simmy.Latency;
using Polly.Simmy.Outcomes;
using RestClient.Shared.Entities;
using System.Net;

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
        ResiliencePipeline<T> BuildPipeline<T>(string retryPolicyName);

        /// <summary>
        /// Gets the retry strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicy">The retry policy configuration.</param>
        /// <returns>The retry strategy options.</returns>
        RetryStrategyOptions<T> GetRetryStrategyOptions<T>(RetryPolicyConfiguration retryPolicy);

        /// <summary>
        /// Gets the circuit breaker strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="retryPolicy">The retry policy configuration.</param>
        /// <returns>The  circuit breaker strategy options.</returns>
        CircuitBreakerStrategyOptions<T> GetCircuitBreakerStrategyOptions<T>(RetryPolicyConfiguration retryPolicy);

        /// <summary>
        /// Gets the http chaos fault strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="chaosPolicyConfiguration">The chaos policy configuration.</param>
        /// <returns>The chaos fault strategy options.</returns>
        ChaosFaultStrategyOptions GetHttpChaosFaultStrategyOptions(ChaosPolicyConfiguration chaosPolicyConfiguration);

        /// <summary>
        /// Gets the chaos outcome strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="chaosPolicyConfiguration">The chaos policy configuration.</param>
        /// <returns>The chaos outcome strategy options.</returns>
        ChaosOutcomeStrategyOptions<HttpResponseMessage> GetChaosOutcomeStrategyOptions(ChaosPolicyConfiguration chaosPolicyConfiguration);

        /// <summary>
        /// Gets the chaos latency strategy options based on the provided retry policy configuration.
        /// </summary>
        /// <param name="chaosPolicyConfiguration">The chaos policy configuration.</param>
        /// <returns>The chaos latency strategy options.</returns>
        ChaosLatencyStrategyOptions GetChaosLatencyStrategyOptions(ChaosPolicyConfiguration chaosPolicyConfiguration);
    }

    ///<inheritdoc/>
    public class PipelineBuilder : IPipelineBuilder
    {
        private ILogger<PipelineBuilder> logger;
        private IConfiguration configuration;
        public PipelineBuilder(ILogger<PipelineBuilder> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        ///<inheritdoc/>
        public ResiliencePipeline<T> BuildPipeline<T>(string retryPolicyName)
        {
            var retryPoliciesSection = this.configuration.GetSection("RetryPolicySettings").Get<List<RetryPolicySettings>>();

            var retryPolicy = retryPoliciesSection.FirstOrDefault(policy => policy.Name == retryPolicyName);

            if (retryPolicy == null)
            {
                throw new ArgumentException($"Missing policy configuration {retryPolicyName}");
            }

            var resiliencePipeline = new ResiliencePipelineBuilder<T>();

            resiliencePipeline.AddRetry(GetRetryStrategyOptions<T>(retryPolicy.Policy));

            resiliencePipeline.AddCircuitBreaker<T>(GetCircuitBreakerStrategyOptions<T>(retryPolicy.Policy));

            resiliencePipeline.AddTimeout(TimeSpan.FromSeconds(retryPolicy.Policy.Timeout));

            return resiliencePipeline.Build();
        }

        ///<inheritdoc/>
        public RetryStrategyOptions<T> GetRetryStrategyOptions<T>(RetryPolicyConfiguration retryPolicy)
        {
            Func<RetryPredicateArguments<T>, ValueTask<bool>> shouldHandleForRetry = args =>
            {
                if (args.Outcome.Result == null && args.Outcome.Exception == null)
                {
                    return new ValueTask<bool>(false);
                }

                if (args.Outcome.Result != null && IsSuccessStatusCode(args.Outcome.Result))
                {
                    return new ValueTask<bool>(false);
                }

                var isRetrySettingsProvided = retryPolicy?.Retry != null && retryPolicy.Retry.RetryForHttpCodes != null && retryPolicy.Retry.RetryForExceptions != null;

                // if retry setting nor provided and response is not success status code than execute retry polciy
                if (!isRetrySettingsProvided && !IsSuccessStatusCode(args.Outcome.Result))
                    return new ValueTask<bool>(true);

                if (retryPolicy?.Retry.RetryForHttpCodes != null
                            && args.Outcome.Result != null
                            && IsHttpStatusCodeMatch(args.Outcome.Result, retryPolicy.Retry.RetryForHttpCodes))
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

            return new RetryStrategyOptions<T>
            {
                // Customize and configure the retry logic.
                BackoffType = delayBackoffType,
                MaxRetryAttempts = retryPolicy?.Retry.MaxRetries ?? 0,
                UseJitter = retryPolicy?.Retry.UseJitter ?? false,
                ShouldHandle = shouldHandleForRetry,
                OnRetry = args =>
                {
                    logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Retry #{args.AttemptNumber}, Duration of attempt {args.Duration.TotalSeconds} seconds. Exception: {args.Outcome.Exception?.Message} Status: {GetStatusCode(args.Outcome.Result)}");
                    return new ValueTask();
                }
            };
        }

        ///<inheritdoc/>
        public CircuitBreakerStrategyOptions<T> GetCircuitBreakerStrategyOptions<T>(RetryPolicyConfiguration retryPolicy)
        {
            var faultTolerancePolicy = retryPolicy?.FaultTolerancePolicy;
            var failureThreshold = faultTolerancePolicy?.FailureThreshold ?? 0;
            var samplingDuration = TimeSpan.FromSeconds(faultTolerancePolicy?.SamplingDurationSeconds ?? 0);
            var breakDurationSeconds = TimeSpan.FromSeconds(faultTolerancePolicy?.BreakDurationSeconds ?? 0);
            var minThroughPut = faultTolerancePolicy?.MinThroughPut ?? 0;

            Func<CircuitBreakerPredicateArguments<T>, ValueTask<bool>> shouldHandle = args =>
            {
                if (args.Outcome.Result == null && args.Outcome.Exception == null)
                {
                    return new ValueTask<bool>(false);
                }

                if (args.Outcome.Result != null && IsSuccessStatusCode(args.Outcome.Result))
                {
                    return new ValueTask<bool>(false);
                }

                var isCircuitBreakerSettingsProvided = faultTolerancePolicy != null
                                                        && faultTolerancePolicy.OpenCircuitForHttpCodes != null
                                                        && faultTolerancePolicy.OpenCircuitForExceptions != null;

                // Check for non-successful status code
                if (!isCircuitBreakerSettingsProvided && !IsSuccessStatusCode(args.Outcome.Result))
                    return new ValueTask<bool>(true);

                if (faultTolerancePolicy?.OpenCircuitForHttpCodes != null
                            && args.Outcome.Result != null
                            && IsHttpStatusCodeMatch(args.Outcome.Result, faultTolerancePolicy.OpenCircuitForHttpCodes))
                    return new ValueTask<bool>(true);

                // Check for HttpRequestException
                if (faultTolerancePolicy?.OpenCircuitForExceptions != null
                         && args.Outcome.Exception != null
                         && faultTolerancePolicy.OpenCircuitForExceptions.Contains(args.Outcome.Exception.GetType().FullName ?? string.Empty))
                    return new ValueTask<bool>(true);

                // Default: do not handle
                return new ValueTask<bool>(false);
            };

            return new CircuitBreakerStrategyOptions<T>
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
                    logger.LogInformation($"Circuit Opened. Break duration {args.BreakDuration}, IsManual {args.IsManual},  Exception: {args.Outcome.Exception?.Message} Status: {GetStatusCode(args.Outcome.Result)} ");
                    return new ValueTask();
                },
                OnClosed = args =>
                {
                    logger.LogInformation($"Circuit Breaker OnClosed.IsManual {args.IsManual},  Exception: {args.Outcome.Exception?.Message} Status: {GetStatusCode(args.Outcome.Result)} ");
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
        public ChaosFaultStrategyOptions GetHttpChaosFaultStrategyOptions(ChaosPolicyConfiguration chaosPolicyConfiguration)
        {
            if (chaosPolicyConfiguration == null)
            {
                return new ChaosFaultStrategyOptions()
                {
                    InjectionRate = 0
                };
            }
            var exception = chaosPolicyConfiguration.GetException();

            return new ChaosFaultStrategyOptions()
            {
                InjectionRate = chaosPolicyConfiguration.InjectionRate,
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
        public ChaosOutcomeStrategyOptions<HttpResponseMessage> GetChaosOutcomeStrategyOptions(ChaosPolicyConfiguration chaosPolicyConfiguration)
        {
            if (chaosPolicyConfiguration == null)
            {
                return new ChaosOutcomeStrategyOptions<HttpResponseMessage>
                {
                    InjectionRate = 0,
                };
            }


            if (!Enum.IsDefined(typeof(HttpStatusCode), chaosPolicyConfiguration.StatusCode))
            {
                throw new ArgumentException($"Invalid outcome (status code) provided. status code provided {chaosPolicyConfiguration.StatusCode}");
            }

            HttpStatusCode httpStatusCode = (HttpStatusCode)chaosPolicyConfiguration.StatusCode;

            return new ChaosOutcomeStrategyOptions<HttpResponseMessage>()
            {
                InjectionRate = chaosPolicyConfiguration.InjectionRate,
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
        public ChaosLatencyStrategyOptions GetChaosLatencyStrategyOptions(ChaosPolicyConfiguration chaosPolicyConfiguration)
        {
            if (chaosPolicyConfiguration == null)
            {
                return new ChaosLatencyStrategyOptions
                {
                    InjectionRate = 0,
                    Latency = TimeSpan.Zero
                };
            }

            if (chaosPolicyConfiguration.LatencySeconds < 0)
            {
                throw new ArgumentException($"Invalid Latency seconds provided.Latencey seconds provided {chaosPolicyConfiguration.LatencySeconds}");
            }

            return new ChaosLatencyStrategyOptions
            {
                InjectionRate = chaosPolicyConfiguration.InjectionRate,
                Latency = TimeSpan.FromSeconds(chaosPolicyConfiguration.LatencySeconds),
                OnLatencyInjected = args =>
                {
                    logger.LogInformation($"OnLatencyInjected, Latency: {args.Latency}, Operation: {args.Context.OperationKey}.");
                    return default;
                }
            };
        }

        private bool IsSuccessStatusCode<T>(T result)
        {
            if (result is HttpResponseMessage responseMessage)
            {
                return responseMessage.IsSuccessStatusCode;
            }

            return false; // Adjust this based on other type T checks
        }

        private string GetStatusCode<T>(T result)
        {
            if (result is HttpResponseMessage responseMessage)
            {
                return responseMessage.StatusCode.ToString();
            }

            return "Unknown"; // Adjust this based on other type T checks
        }

        private bool IsHttpStatusCodeMatch<T>(T result, List<int> retryForHttpCodes)
        {
            if (result is HttpResponseMessage responseMessage)
            {
                return retryForHttpCodes.Contains((int)responseMessage.StatusCode);
            }

            return false; // Adjust this based on other type T checks
        }
    }
}

