using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OrderManagement.Api.Auth
{
    public static class ApiKeyAuthConstants
    {
        public const string HeaderName = "X-Api-Key";
        public const string SchemeName = "ApiKey";
    }

    public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {

    }

    // Draft of class that validates X-Api-Key header against a partner registry
    public class ApiKeyAuthenticationHandler: AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
    {
        public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
                                           ILoggerFactory logger,
                                           UrlEncoder encoder): base(options, logger, encoder)
        {

        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if(!Request.Headers.TryGetValue(ApiKeyAuthConstants.HeaderName, out var providedKey) || string.IsNullOrWhiteSpace(providedKey))
            {
                return Task.FromResult(AuthenticateResult.Fail($"Missing {ApiKeyAuthConstants.HeaderName} header."));
            }

            var partner = ValidateApiKey(providedKey!);
            if (partner is null)
            {
                return Task.FromResult(AuthenticateResult.Fail($"Invalid API key"));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, partner.Value.PartnerId),
                new Claim("partner_name", partner.Value.Name)
            };

            var identity = new ClaimsIdentity(claims, ApiKeyAuthConstants.SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, ApiKeyAuthConstants.SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));

        }

        private (string PartnerId, string Name)? ValidateApiKey(string apiKey)
        {
            // TODO: replace with actual validation against a partner registry
            return null;
        }
    }
}
