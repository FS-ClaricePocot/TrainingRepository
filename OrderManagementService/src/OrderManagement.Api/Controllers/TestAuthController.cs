using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderManagement.Api.Controllers
{
    // TEST-ONLY - exists purely so the cookie/admin auth path can be
    // verified end-to-end (e.g. in Postman) without a real login flow.
    // Not part of the reviewed design. Gated for Development only. 
    [ApiController]
    [Route("api/test")]
    public class TestAuthController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public TestAuthController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("admin-login")]
        [AllowAnonymous]
        public async Task<IActionResult> AdminLogin()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-admin-1") };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Ok(new { message = "Signed in as test admin" });
        }
    }
}
