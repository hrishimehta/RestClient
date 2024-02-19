using Microsoft.AspNetCore.Mvc;
using RestClient.Infrastructure.Services;

namespace RestClient.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RestRetryController : ControllerBase
    {
        private readonly ChuckNorrisService _chuckNorrisService;

        public RestRetryController(ChuckNorrisService chuckNorrisService)
        {
            _chuckNorrisService = chuckNorrisService;
        }

        [HttpGet("random-joke")]
        public async Task<IActionResult> GetRandomJoke()
        {
            try
            {
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
