using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.Registry;
using RestClient.API.Extension;
using RestClient.Domain;
using RestClient.Infrastructure.Services;
using System.Diagnostics.CodeAnalysis;

namespace RestClient.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ExcludeFromCodeCoverage]
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
        public IActionResult MongoCall()
        {
            try
            {
                var mongoResilience = this.pipelineBuilder.BuildPipeline<Employee>("MongoRetryPolicy");

                var result = mongoResilience.Execute<Employee>(() =>
                {
                    return this.GetEmployeeDataFromMongo();
                });
                Console.WriteLine("Employee info" + result.Id + "," + result.Name);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Handle exceptions, log errors, etc.
                return StatusCode(500, "Internal Server Error");
            }
        }

        public record Employee
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private Employee GetEmployeeDataFromMongo()
        {
            try
            {
                //throw new Exception();

                return new Employee()
                {
                    Id = 1,
                    Name = "test"
                };
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}
