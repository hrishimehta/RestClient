using Microsoft.AspNetCore.Mvc;
using RestClient.Domain;

namespace RestClient.API.Controllers
{
    public class SampleController : ControllerBase
    {

        [HttpGet("TestEndPoint")]
        public IActionResult TestEndPoint()
        {
            return Ok(new ChuckNorrisJoke() { Value = "Testvalue" });
        }
    }
}
