using Microsoft.AspNetCore.Authentication.Cookies;

namespace OrderManagement.Api.Auth
{
    // The mechanism that tells checks which scheme to use
    public static class AuthSchemeSelector
    {
        public static string Select(HttpContext context)
        {
            return context.Request.Headers.ContainsKey(ApiKeyAuthConstants.HeaderName) 
                ? ApiKeyAuthConstants.SchemeName 
                : CookieAuthenticationDefaults.AuthenticationScheme;
        }
    }
}
