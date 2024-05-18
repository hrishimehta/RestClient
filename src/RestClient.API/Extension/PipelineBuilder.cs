using Microsoft.Extensions.Http.Resilience;
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
    public static class PipelineBuilder
    {
        public static ResiliencePipeline<HttpResponseMessage> BuildPipeline(RetryPolicyConfiguration retryPolicy, ILogger logger)
        {
            var resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>();

            resiliencePipeline.AddRetry(GetHttpRetryStrategyOptions(retryPolicy, logger));

            resiliencePipeline.AddCircuitBreaker(GetHttpCircuitBreakerStrategyOptions(retryPolicy, logger));

            resiliencePipeline.AddTimeout(TimeSpan.FromSeconds(retryPolicy?.Timeout.TimeoutDuration ?? 0));

            return resiliencePipeline.Build();
        }

        public static HttpRetryStrategyOptions GetHttpRetryStrategyOptions(RetryPolicyConfiguration retryPolicy, ILogger logger)
        {
            Func<RetryPredicateArguments<HttpResponseMessage>, ValueTask<bool>> shouldHandleForRetry = args =>
            {
                if (args.Outcome.Result == null)
                {
                    return new ValueTask<bool>(false);
                }

                var isRetrySettingsProvided = retryPolicy?.Retry != null && retryPolicy.Retry.RetryForHttpCodes != null && retryPolicy.Retry.RetryForExceptions != null;

                // if retry setting nor provided and response is not success status code than execute retry polciy
                if (!isRetrySettingsProvided && !args.Outcome.Result.IsSuccessStatusCode)
                    return new ValueTask<bool>(true);

                if (retryPolicy?.Retry.RetryForHttpCodes != null
                            && retryPolicy.Retry.RetryForHttpCodes.Contains((int)args.Outcome.Result.StatusCode))
                    return new ValueTask<bool>(true);

                // Check for HttpRequestException
                if (retryPolicy?.Retry.RetryForExceptions != null
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

        public static HttpCircuitBreakerStrategyOptions GetHttpCircuitBreakerStrategyOptions(RetryPolicyConfiguration retryPolicy, ILogger logger)
        {
            var faultTolerancePolicy = retryPolicy?.FaultTolerancePolicy;
            var failureThreshold = faultTolerancePolicy?.FailureThreshold ?? 0;
            var samplingDuration = TimeSpan.FromSeconds(faultTolerancePolicy?.SamplingDurationSeconds ?? 0);
            var breakDurationSeconds = TimeSpan.FromSeconds(faultTolerancePolicy?.BreakDurationSeconds ?? 0);
            var minThroughPut = faultTolerancePolicy?.MinThroughPut ?? 0;

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

            return new HttpCircuitBreakerStrategyOptions
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
            };
        }

        public static ChaosFaultStrategyOptions GetChaosFaultStrategyOptions(RetryPolicyConfiguration retryPolicy, ILogger logger)
        {
            return new ChaosFaultStrategyOptions()
            {
                InjectionRate = 0.1,
                OnFaultInjected = args =>
                {
                    logger.LogInformation($"OnFaultInjected, Exception: {args.Fault.Message}, Operation: {args.Context.OperationKey}.");
                    return default;
                }
            };
        }

        public static ChaosOutcomeStrategyOptions<HttpResponseMessage> GetChaosOutcomeStrategyOptions(RetryPolicyConfiguration retryPolicy, ILogger logger)
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

        public static ChaosLatencyStrategyOptions GetChaosLatencyStrategyOptions(RetryPolicyConfiguration retryPolicy, ILogger logger)
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

