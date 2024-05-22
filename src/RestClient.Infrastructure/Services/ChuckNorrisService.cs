using Microsoft.Extensions.Logging;
using RestClient.Domain;
using System.Net.Http.Json;

namespace RestClient.Infrastructure.Services
{
    public class ChuckNorrisService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChuckNorrisService> _logger;
        public ChuckNorrisService(IHttpClientFactory httpClientFactory,ILogger<ChuckNorrisService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ChuckNorrisJoke> GetRandomJoke()
        {
            try
            {
                _logger.LogWarning($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - First call");
                var httpClient = _httpClientFactory.CreateClient("ChuckNorrisService");
                // change /jokes/random1 for working solution
                var jokeResponse = await httpClient.GetFromJsonAsync<ChuckNorrisJoke>("https://localhost:7082/TestEndPoint"); 
                return jokeResponse;
            }
            catch (HttpRequestException ex)
            {
                // Handle specific HTTP request exceptions
                throw new Exception("Error fetching Chuck Norris joke", ex);
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                throw new Exception("An error occurred while processing the request", ex);
            }
        }
    }
}
