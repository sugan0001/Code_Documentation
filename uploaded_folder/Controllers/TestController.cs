using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Newtonsoft.Json;

namespace COR.Profile.Api.Controllers
{

    [Route("[controller]")]
    [ApiController]
    [EnableRateLimiting("fixed")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        [Authorize]
        [Route("ProfileList")]
        public string ProfileList()
        {

            var re = Request;
            var headers = re.Headers;

            var jwtToken = new JwtSecurityToken();
            var id = jwtToken.Subject;
            return id.ToString();
        }
        [HttpGet]
        [Authorize("Bearer")]
        public string GetUserInfo()
        {
            var claimsIdentity = User.Identity as ClaimsIdentity;

            return JsonConvert.SerializeObject(new
            {
                UserName = claimsIdentity.Name
            });
        }
    }
}