using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using RestClient.API.Extension;
using RestClient.Shared.Entities;
using Xunit;

namespace RestClient.API.Tests.Extensions
{
    public class HttpClientRetryExtensionsTests
    {
        [Fact]
        public void AddHttpClientWithRetryPolicy_Should_Add_HttpClient_With_Exponential_Retry_Policy_When_Configured()
        {
            // Arrange
            var systemName = "ChuckNorrisService";
            var services = new ServiceCollection();

            // Build the configuration from the testsettings.json file
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())  // Adjust the path based on your project structure
                .AddJsonFile("testsettings.json")
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<ILogger>(new Mock<ILogger>().Object);

            // Act
            services.AddHttpClientWithRetryPolicy(systemName, services.BuildServiceProvider().GetRequiredService<ILogger>());

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(systemName);
            Assert.NotNull(httpClient);
        }

        [Fact]
        public void AddHttpClientWithRetryPolicy_Should_Add_HttpClient_With_Default_Policy_When_No_Configuration()
        {
            // Arrange
            var systemName = "DefaultService";
            var services = new ServiceCollection();

            // No configuration provided
            // Build the configuration from the testsettings.json file
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())  // Adjust the path based on your project structure
                .AddJsonFile("testsettings.json")
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<ILogger>(new Mock<ILogger>().Object);

            // Act
            services.AddHttpClientWithRetryPolicy(systemName, new Mock<ILogger>().Object);

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(systemName);
            Assert.NotNull(httpClient);
        }

        [Fact]
        public void AddHttpClientWithRetryPolicy_Should_Add_HttpClient_With_Constant_Retry_Policy_When_Configured()
        {
            // Arrange
            var systemName = "System2";
            var services = new ServiceCollection();

            // Build the configuration from the testsettings.json file
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())  // Adjust the path based on your project structure
                .AddJsonFile("testsettings.json")
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<ILogger>(new Mock<ILogger>().Object);

            // Act
            services.AddHttpClientWithRetryPolicy(systemName, services.BuildServiceProvider().GetRequiredService<ILogger>());

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(systemName);
            Assert.NotNull(httpClient);
        }

        [Fact]
        public void GetExponentialRetryPolicy_ConfiguresExponentialRetryPolicy()
        {
            // Arrange
            var faultTolerancePolicy = new FaultTolerancePolicy { Enabled = true, FailureThreshold = 0.5, SamplingDurationSeconds = 2 };
            var maxRetries = 3;
            var exponentialBase = 2;
            var services = new ServiceCollection();
            services.AddSingleton<ILogger>(new Mock<ILogger>().Object);
            // Act
            var retryPolicy = HttpClientRetryExtensions.GetExponentialRetryPolicy(maxRetries, exponentialBase, faultTolerancePolicy, services.BuildServiceProvider().GetRequiredService<ILogger>());

            // Assert
            Assert.IsAssignableFrom<IAsyncPolicy<HttpResponseMessage>>(retryPolicy);
        }

        [Fact]
        public void GetConstantRetryPolicy_ConfiguresConstantRetryPolicy()
        {
            // Arrange
            var faultTolerancePolicy = new FaultTolerancePolicy { Enabled = true, FailureThreshold = 0.5, SamplingDurationSeconds = 2 };
            var maxRetries = 3;
            var retryInterval = 5;
            var services = new ServiceCollection();
            services.AddSingleton<ILogger>(new Mock<ILogger>().Object);
            // Act
            var retryPolicy = HttpClientRetryExtensions.GetConstantRetryPolicy(maxRetries, retryInterval, faultTolerancePolicy, services.BuildServiceProvider().GetRequiredService<ILogger>());

            // Assert
            Assert.IsAssignableFrom<IAsyncPolicy<HttpResponseMessage>>(retryPolicy);
        }

        [Fact]
        public void ApplyCircuitBreaker_ConfiguresCircuitBreakerPolicy()
        {
            // Arrange
            var faultTolerancePolicy = new FaultTolerancePolicy { Enabled = true, FailureThreshold = 0.5, SamplingDurationSeconds = 2 };
            var logger = Mock.Of<ILogger>();

            // Act
            var circuitBreakerPolicy = HttpClientRetryExtensions.ApplyCircuitBreaker(faultTolerancePolicy, logger);

            // Assert
            Assert.IsAssignableFrom<IAsyncPolicy<HttpResponseMessage>>(circuitBreakerPolicy);
        }

        [Fact]
        public void CalculateRetryDelay_ReturnsCorrectDelay()
        {
            // Arrange
            var exponentialBase = 2;
            var retryAttempt = 3;
            var faultTolerancePolicy = new FaultTolerancePolicy { Enabled = true, FailureThreshold = 0.5, SamplingDurationSeconds = 2 };

            // Act
            var retryDelay = HttpClientRetryExtensions.CalculateRetryDelay(exponentialBase, retryAttempt, faultTolerancePolicy);

            // Assert
            Assert.True(retryDelay >= TimeSpan.Zero);
        }

        [Fact]
        public void GetJitter_ReturnsCorrectJitter()
        {
            // Arrange
            var baseValue = 100;
            var jitterStrategy = new JitterStrategy { Enabled = true, Percentage = 20 };

            // Act
            var jitter = HttpClientRetryExtensions.GetJitter(baseValue, jitterStrategy);

            // Assert
            Assert.True(jitter.TotalMilliseconds >= -baseValue * 0.2 && jitter.TotalMilliseconds <= baseValue * 0.2);
        }

        [Fact]
        public void GetJitter_ReturnsZeroWhenDisableJitter()
        {
            // Arrange
            var baseValue = 100;
            var jitterStrategy = new JitterStrategy { Enabled = false, Percentage = 20 };

            // Act
            var jitter = HttpClientRetryExtensions.GetJitter(baseValue, jitterStrategy);

            // Assert
            Assert.Equal(TimeSpan.Zero, jitter);
        }


        [Fact]
        public void GetJitter_ReturnsZeroWhenNullJitter()
        {
            // Arrange
            var baseValue = 100;

            // Act
            var jitter = HttpClientRetryExtensions.GetJitter(baseValue, null);

            // Assert
            Assert.Equal(TimeSpan.Zero, jitter);
        }
    }
}
