using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RestClient.API.Extension;
using RestClient.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using Polly.CircuitBreaker;
using Polly;
using Polly.Retry;

namespace RestClient.API.Tests.Extensions
{
    public class PipelineBuilderTests
    {
        private IConfiguration GetConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("testsettings.json")
                .Build();
            return config;
        }

        [Fact]
        public void BuildPipeline_ShouldReturnResiliencePipeline_WhenRetryPolicyFound()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var configurationMock = new Mock<IConfiguration>();
            configurationMock.Setup(x => x.GetSection("RetryPolicySettings").GetChildren())
                             .Returns(GetConfiguration().GetSection("RetryPolicySettings").GetChildren());

            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, configurationMock.Object);

            // Act
            var resiliencePipeline = pipelineBuilder.BuildPipeline<HttpResponseMessage>("TestPolicy");

            // Assert
            Assert.NotNull(resiliencePipeline);
            // Add more assertions to validate the resilience pipeline configuration
        }

        [Fact]
        public void GetRetryStrategyOptions_Should_Return_RetryStrategyOptions_When_RetryPolicy_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var configurationMock = new Mock<IConfiguration>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, configurationMock.Object);
            var retryPolicy = new RetryPolicyConfiguration
            {
                Retry = new RetryPolicy
                {
                    MaxRetries = 3,
                    RetryForHttpCodes = new List<int> { 500 },
                    RetryForExceptions = new List<string> { "System.Net.Http.HttpRequestException" }
                }
            };

            // Act
            var retryStrategyOptions = pipelineBuilder.GetRetryStrategyOptions<HttpResponseMessage>(retryPolicy);

            // Assert
            Assert.NotNull(retryStrategyOptions);
            // Add more assertions to verify the expected behavior of GetRetryStrategyOptions method
        }

        [Fact]
        public void GetRetryStrategyOptions_Should_Return_Default_RetryStrategyOptions_When_RetryPolicy_Not_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var configurationMock = new Mock<IConfiguration>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, configurationMock.Object);

            // Act
            var retryStrategyOptions = pipelineBuilder.GetRetryStrategyOptions<HttpResponseMessage>(null);

            // Assert
            Assert.NotNull(retryStrategyOptions);
            // Add assertions to verify the default behavior of GetRetryStrategyOptions method
        }

        [Fact]
        public async Task ShouldHandle_ReturnsFalse_WhenOutcomeIsNullAndRetryForHttpCodesContainsNull()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                Retry = new RetryPolicy
                {
                    RetryForHttpCodes = null
                }
            };
            var retryStrategyOptions = pipelineBuilder.GetRetryStrategyOptions<HttpResponseMessage>(retryPolicy);
            var arguments = new RetryPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult<HttpResponseMessage>(null), 0);

            var result = await retryStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsFalse_WhenOutcomeIsSuccessStatusCodeAndRetryForHttpCodesContainsStatusCode()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                Retry = new RetryPolicy
                {
                    RetryForHttpCodes = new List<int> { (int)HttpStatusCode.InternalServerError }
                }
            };
            var retryStrategyOptions = pipelineBuilder.GetRetryStrategyOptions<HttpResponseMessage>(retryPolicy);

            var arguments = new RetryPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.OK)), 0);
            var result = await retryStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsTrue_WhenOutcomeIsInternalServerErrorCodeAndRetryForHttpCodesContainsStatusCode()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                Retry = new RetryPolicy
                {
                    RetryForHttpCodes = new List<int> { (int)HttpStatusCode.InternalServerError }
                }
            };
            var retryStrategyOptions = pipelineBuilder.GetRetryStrategyOptions<HttpResponseMessage>(retryPolicy);

            var arguments = new RetryPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)), 0);
            var result = await retryStrategyOptions.ShouldHandle(arguments);

            Assert.True(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsFalse_WhenOutcomeIsNotFoundCodeAndRetryForHttpCodes_NotContains_StatusCode()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                Retry = new RetryPolicy
                {
                    RetryForHttpCodes = new List<int> { (int)HttpStatusCode.InternalServerError }
                }
            };
            var retryStrategyOptions = pipelineBuilder.GetRetryStrategyOptions<HttpResponseMessage>(retryPolicy);

            var arguments = new RetryPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)), 1);
            var result = await retryStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsFalse_WhenOutcomeException_AndRetryForExceptions_NotContains_Exception()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                Retry = new RetryPolicy
                {
                    RetryForExceptions = new List<string> { typeof(InvalidOperationException).FullName },
                }
            };
            var retryStrategyOptions = pipelineBuilder.GetRetryStrategyOptions<Exception>(retryPolicy);

            var arguments = new RetryPredicateArguments<Exception>(null, Outcome.FromException<Exception>(new Exception()), 2);
            var result = await retryStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsTrue_WhenOutcomeException_AndOpenCircuitForHttpCodes_Contains_Exception()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                FaultTolerancePolicy = new FaultTolerancePolicy
                {
                    OpenCircuitForExceptions = new List<string> { typeof(InvalidOperationException).FullName },
                }
            };
            var circuitBreakerStrategyOptions = pipelineBuilder.GetCircuitBreakerStrategyOptions<InvalidOperationException>(retryPolicy);

            var arguments = new CircuitBreakerPredicateArguments<InvalidOperationException>(null, Outcome.FromException<InvalidOperationException>(new InvalidOperationException()));
            var result = await circuitBreakerStrategyOptions.ShouldHandle(arguments);

            Assert.True(result);
        }

        [Fact]
        public void GetCircuitBreakerStrategyOptions_Should_Return_CircuitBreakerStrategyOptions_When_RetryPolicy_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var configurationMock = new Mock<IConfiguration>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, configurationMock.Object);
            var retryPolicy = new RetryPolicyConfiguration
            {
                FaultTolerancePolicy = new FaultTolerancePolicy
                {
                    FailureThreshold = 0.5,
                    SamplingDurationSeconds = 10,
                    BreakDurationSeconds = 60,
                    MinThroughPut = 10,
                    OpenCircuitForHttpCodes = new List<int> { 404 },
                    OpenCircuitForExceptions = new List<string> { "System.Net.Http.HttpRequestException" }
                }
            };

            // Act
            var circuitBreakerStrategyOptions = pipelineBuilder.GetCircuitBreakerStrategyOptions<HttpResponseMessage>(retryPolicy);
            // Assert
            Assert.NotNull(circuitBreakerStrategyOptions);
            // Add more assertions to verify the expected behavior of GetCircuitBreakerStrategyOptions method
        }

        [Fact]
        public async Task ShouldHandle_ReturnsTrue_WhenOutcomeIsNullAndOpenCircuitForHttpCodesContainsNull()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                FaultTolerancePolicy = new FaultTolerancePolicy
                {
                    OpenCircuitForHttpCodes = null
                }
            };
            var circuitBreakerStrategyOptions = pipelineBuilder.GetCircuitBreakerStrategyOptions<HttpResponseMessage>(retryPolicy);
            var arguments = new CircuitBreakerPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult<HttpResponseMessage>(null));

            var result = await circuitBreakerStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsFalse_WhenOutcomeIsSuccessStatusCodeAndOpenCircuitForHttpCodesContainsStatusCode()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                FaultTolerancePolicy = new FaultTolerancePolicy
                {
                    OpenCircuitForHttpCodes = new List<int> { (int)HttpStatusCode.InternalServerError }
                }
            };
            var circuitBreakerStrategyOptions = pipelineBuilder.GetCircuitBreakerStrategyOptions<HttpResponseMessage>(retryPolicy);

            var arguments = new CircuitBreakerPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var result = await circuitBreakerStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsTrue_WhenOutcomeIsInternalServerErrorCodeAndOpenCircuitForHttpCodesContainsStatusCode()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                FaultTolerancePolicy = new FaultTolerancePolicy
                {
                    OpenCircuitForHttpCodes = new List<int> { (int)HttpStatusCode.InternalServerError }
                }
            };
            var circuitBreakerStrategyOptions = pipelineBuilder.GetCircuitBreakerStrategyOptions<HttpResponseMessage>(retryPolicy);

            var arguments = new CircuitBreakerPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            var result = await circuitBreakerStrategyOptions.ShouldHandle(arguments);

            Assert.True(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsFalse_WhenOutcomeIsNotFoundCodeAndOpenCircuitForHttpCodes_NotContains_StatusCode()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                FaultTolerancePolicy = new FaultTolerancePolicy
                {
                    OpenCircuitForHttpCodes = new List<int> { (int)HttpStatusCode.InternalServerError }
                }
            };
            var circuitBreakerStrategyOptions = pipelineBuilder.GetCircuitBreakerStrategyOptions<HttpResponseMessage>(retryPolicy);

            var arguments = new CircuitBreakerPredicateArguments<HttpResponseMessage>(null, Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
            var result = await circuitBreakerStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldHandle_ReturnsFalse_WhenOutcomeException_AndOpenCircuitForHttpCodes_NotContains_Exception()
        {
            var pipelineBuilder = new PipelineBuilder(Mock.Of<ILogger<PipelineBuilder>>(), Mock.Of<IConfiguration>());
            var retryPolicy = new RetryPolicyConfiguration
            {
                // Set up RetryPolicyConfiguration with specific conditions
                FaultTolerancePolicy = new FaultTolerancePolicy
                {
                    OpenCircuitForExceptions = new List<string> { typeof(InvalidOperationException).FullName },
                }
            };
            var circuitBreakerStrategyOptions = pipelineBuilder.GetCircuitBreakerStrategyOptions<Exception>(retryPolicy);

            var arguments = new CircuitBreakerPredicateArguments<Exception>(null, Outcome.FromException<Exception>(new Exception()));
            var result = await circuitBreakerStrategyOptions.ShouldHandle(arguments);

            Assert.False(result);
        }

      
        [Fact]
        public void GetHttpChaosFaultStrategyOptions_Should_Return_Default_ChaosFaultStrategyOptions_When_ChaosPolicyConfiguration_Not_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, null);

            // Act
            var chaosFaultStrategyOptions = pipelineBuilder.GetHttpChaosFaultStrategyOptions(null);

            // Assert
            Assert.NotNull(chaosFaultStrategyOptions);
            Assert.Equal(0, chaosFaultStrategyOptions.InjectionRate);
            Assert.Null(chaosFaultStrategyOptions.FaultGenerator);
            Assert.Null(chaosFaultStrategyOptions.OnFaultInjected);
        }

        [Fact]
        public void GetChaosOutcomeStrategyOptions_Should_Return_Default_ChaosOutcomeStrategyOptions_When_ChaosPolicyConfiguration_Not_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, null);

            // Act
            var chaosOutcomeStrategyOptions = pipelineBuilder.GetChaosOutcomeStrategyOptions(null);

            // Assert
            Assert.NotNull(chaosOutcomeStrategyOptions);
            Assert.Equal(0, chaosOutcomeStrategyOptions.InjectionRate);
            Assert.Null(chaosOutcomeStrategyOptions.OutcomeGenerator);
            Assert.Null(chaosOutcomeStrategyOptions.OnOutcomeInjected);
        }

        [Fact]
        public void GetChaosLatencyStrategyOptions_Should_Return_Default_ChaosLatencyStrategyOptions_When_ChaosPolicyConfiguration_Not_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, null);

            // Act
            var chaosLatencyStrategyOptions = pipelineBuilder.GetChaosLatencyStrategyOptions(null);

            // Assert
            Assert.NotNull(chaosLatencyStrategyOptions);
            Assert.Equal(0, chaosLatencyStrategyOptions.InjectionRate);
            Assert.Equal(TimeSpan.Zero, chaosLatencyStrategyOptions.Latency);
            Assert.Null(chaosLatencyStrategyOptions.OnLatencyInjected);
        }

        [Fact]
        public void GetHttpChaosFaultStrategyOptions_Should_Return_Correct_ChaosFaultStrategyOptions_When_ChaosPolicyConfiguration_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, null);
            var chaosPolicyConfiguration = new ChaosPolicyConfiguration
            {
                InjectionRate = 0.5,
                Fault = typeof(InvalidOperationException).FullName
            };

            // Act
            var chaosFaultStrategyOptions = pipelineBuilder.GetHttpChaosFaultStrategyOptions(chaosPolicyConfiguration);

            // Assert
            Assert.NotNull(chaosFaultStrategyOptions);
            Assert.Equal(0.5, chaosFaultStrategyOptions.InjectionRate);
            Assert.NotNull(chaosFaultStrategyOptions.FaultGenerator);
            Assert.NotNull(chaosFaultStrategyOptions.OnFaultInjected);
        }

        [Fact]
        public void GetChaosOutcomeStrategyOptions_Should_Return_Correct_ChaosOutcomeStrategyOptions_When_ChaosPolicyConfiguration_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, null);
            var chaosPolicyConfiguration = new ChaosPolicyConfiguration
            {
                InjectionRate = 0.7,
                StatusCode = (int)HttpStatusCode.InternalServerError
            };

            // Act
            var chaosOutcomeStrategyOptions = pipelineBuilder.GetChaosOutcomeStrategyOptions(chaosPolicyConfiguration);

            // Assert
            Assert.NotNull(chaosOutcomeStrategyOptions);
            Assert.Equal(0.7, chaosOutcomeStrategyOptions.InjectionRate);
            Assert.NotNull(chaosOutcomeStrategyOptions.OutcomeGenerator);
            Assert.NotNull(chaosOutcomeStrategyOptions.OnOutcomeInjected);
        }

        [Fact]
        public void GetChaosLatencyStrategyOptions_Should_Return_Correct_ChaosLatencyStrategyOptions_When_ChaosPolicyConfiguration_Provided()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PipelineBuilder>>();
            var pipelineBuilder = new PipelineBuilder(loggerMock.Object, null);
            var chaosPolicyConfiguration = new ChaosPolicyConfiguration
            {
                InjectionRate = 0.3,
                LatencySeconds = 5
            };

            // Act
            var chaosLatencyStrategyOptions = pipelineBuilder.GetChaosLatencyStrategyOptions(chaosPolicyConfiguration);

            // Assert
            Assert.NotNull(chaosLatencyStrategyOptions);
            Assert.Equal(0.3, chaosLatencyStrategyOptions.InjectionRate);
            Assert.Equal(TimeSpan.FromSeconds(5), chaosLatencyStrategyOptions.Latency);
            Assert.NotNull(chaosLatencyStrategyOptions.OnLatencyInjected);
        }

    }
}
