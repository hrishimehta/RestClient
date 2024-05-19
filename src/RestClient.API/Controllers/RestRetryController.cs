using Microsoft.AspNetCore.Mvc;
using Polly.Registry;
using RestClient.API.Extension;
using RestClient.Domain;
using RestClient.Infrastructure.Services;

namespace RestClient.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RestRetryController : ControllerBase
    {
        private readonly ChuckNorrisService _chuckNorrisService;
        private readonly IPipelineBuilder pipelineBuilder;

        public RestRetryController(ChuckNorrisService chuckNorrisService, IPipelineBuilder pipelineBuilder)
        {
            this.pipelineBuilder = pipelineBuilder;
            _chuckNorrisService = chuckNorrisService;
        }

        [HttpGet("random-joke")]
        public async Task<IActionResult> GetRandomJoke()
        {
            try
            {
                // Fetch pipeline "A"
                //var registry = new ResiliencePipelineRegistry<string>();
                //Polly.ResiliencePipeline pipelineA = registry.GetPipeline("ChuckNorrisServiceRetryPolicy");

                var joke = await _chuckNorrisService.GetRandomJoke();
                return Ok(joke.Value);
            }
            catch (Exception ex)
            {
                // Handle exceptions, log errors, etc.
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("mongoCall")]
        public async Task<IActionResult> MongoCall()
        {
            try
            {
                var mongoResilience = this.pipelineBuilder.BuildPipeline("ChuckNorrisServiceRetryPolicy");

                //mongoResilience.ExecuteAsync(async () => await _chuckNorrisService.GetRandomJoke());

                //mongoRessiliency.ExecuteAsync<ChuckNorrisJoke>( async () =>
                //{
                //    await _chuckNorrisService.GetRandomJoke();
                //});
                var joke = await _chuckNorrisService.GetRandomJoke();
                return Ok(joke.Value);
            }
            catch (Exception ex)
            {
                // Handle exceptions, log errors, etc.
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}
