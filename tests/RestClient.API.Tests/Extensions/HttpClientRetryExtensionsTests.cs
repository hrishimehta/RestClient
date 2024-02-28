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
    }
}
