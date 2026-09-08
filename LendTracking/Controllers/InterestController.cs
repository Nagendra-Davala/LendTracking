using LendTracking.Model;
using LendTracking.Services;
using Microsoft.AspNetCore.Mvc;

namespace LendTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InterestController : ControllerBase
    {
        public InterestController() { }

        [HttpPost("Simple")]
        public SimpleInterestResponse CalculateSimpleInterest([FromBody] SimpleInterestModel model)
        {
            var service = new SimpleInterestService();
            return service.CalculateSimpleInterest(model);
        }
    }
}
