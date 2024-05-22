using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RestClient.API.Extension;
using RestClient.Shared.Entities;

namespace RestClient.API.Tests.Extensions
{
    public class HttpClientRetryExtensionsTests
    {
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IPipelineBuilder> _pipelineBuilderMock;
        private readonly Mock<ILogger> _loggerMock;
        private IConfiguration _configuration;

        public HttpClientRetryExtensionsTests()
        {
            _serviceProviderMock = new Mock<IServiceProvider>();
            _pipelineBuilderMock = new Mock<IPipelineBuilder>();
            _loggerMock = new Mock<ILogger>();
        }

        private IServiceCollection SetupServiceCollection(string settingsJson)
        {
            var services = new ServiceCollection();

            // Build in-memory configuration from JSON
            var configuration = new ConfigurationBuilder()
                .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(settingsJson)))
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(_pipelineBuilderMock.Object);

            _serviceProviderMock.Setup(x => x.GetService(typeof(IConfiguration))).Returns(configuration);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IPipelineBuilder))).Returns(_pipelineBuilderMock.Object);
            services.AddSingleton(_serviceProviderMock.Object);

            _configuration = configuration;

            return services;
        }

        [Fact]
        public void AddHttpClientWithRetryPolicy_ShouldThrowArgumentException_WhenHttpClientSettingsAreNull()
        {
            // Arrange
            var settingsJson = "{}"; // Empty JSON to simulate missing settings
            var services = SetupServiceCollection(settingsJson);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => services.AddHttpClientWithRetryPolicy(_loggerMock.Object));
            Assert.Equal("Httpclient setitng is missing in appsettings file", ex.Message);
        }

        [Fact]
        public void AddHttpClientWithRetryPolicy_ShouldAddHttpClientWithoutRetryPolicies_WhenRetryPolicyIsNull()
        {
            // Arrange
            var settingsJson = @"
        {
            ""HttpClient"": [
                { ""Name"": ""TestClient"", ""RetryPolicyName"": ""NonExistingPolicy"" }
            ]
        }";
            var services = SetupServiceCollection(settingsJson);

            // Act
            services.AddHttpClientWithRetryPolicy(_loggerMock.Object);

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
            Assert.NotNull(httpClientFactory);
        }


        [Fact]
        public void AddHttpClientWithRetryPolicy_ShouldAddHttpClientWithRetryPolicies_WhenRetryPolicyIsNotNull()
        {
            // Arrange
            var settingsJson = @"
        {
            ""HttpClient"": [
                { ""Name"": ""TestClient"", ""RetryPolicyName"": ""TestPolicy"" }
            ],
            ""RetryPolicySettings"": [
                {
                    ""Name"": ""TestPolicy"",
                    ""Policy"": { ""Timeout"": 1 }
                }
            ]
        }";
            var services = SetupServiceCollection(settingsJson);

            // Act
            services.AddHttpClientWithRetryPolicy(_loggerMock.Object);

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
            Assert.NotNull(httpClientFactory);
        }

        [Fact]
        public void AddHttpClientWithRetryPolicy_ShouldApplyChaosPolicies_WhenChaosIsEnabled()
        {
            // Arrange
            var settingsJson = @"
        {
            ""HttpClient"": [
                { ""Name"": ""TestClient"", ""RetryPolicyName"": ""TestPolicy"" }
            ],
            ""RetryPolicySettings"": [
                {
                    ""Name"": ""TestPolicy"",
                    ""Policy"": {
                        ""Timeout"": 1,
                        ""ChaosPolicies"": [
                            { ""Type"": ""Fault"" }
                        ]
                    }
                }
            ],
            ""IsChaosEnabled"": true
        }";
            var services = SetupServiceCollection(settingsJson);

            // Act
            services.AddHttpClientWithRetryPolicy(_loggerMock.Object);

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
            Assert.NotNull(httpClientFactory);
        }
    }
}
