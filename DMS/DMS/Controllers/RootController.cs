using Microsoft.AspNetCore.Mvc;

namespace REST_API.Controllers
{
    [ApiController]
    [Route("")]
    public class RootController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetRoot()
        {
            var data = new { Message = "This is the root endpoint.", Timestamp = DateTime.UtcNow };
            return Ok(data);
        }
    }
}
