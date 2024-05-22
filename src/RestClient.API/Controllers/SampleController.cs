using Microsoft.AspNetCore.Mvc;
using RestClient.Domain;
using System.Diagnostics.CodeAnalysis;

namespace RestClient.API.Controllers
{
    public class SampleController : ControllerBase
    {

        [HttpGet("TestEndPoint")]
        [ExcludeFromCodeCoverage]
        public IActionResult TestEndPoint()
        {
            return Ok(new ChuckNorrisJoke() { Value = "Testvalue" });
        }
    }
}
