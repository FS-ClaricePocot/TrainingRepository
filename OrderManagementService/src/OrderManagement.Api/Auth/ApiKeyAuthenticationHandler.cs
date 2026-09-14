using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OrderManagement.Api.Partners;

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

    public class ApiKeyAuthenticationHandler: AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
    {
        private readonly IPartnerRepository _partnerRepository;

        // Both the old key (Rotating) and new key (Active) validate within this window
        private static readonly TimeSpan _rotationGracePeriod = TimeSpan.FromHours(48);
        public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
                                           ILoggerFactory logger,
                                           UrlEncoder encoder,
                                           IPartnerRepository partnerRepository): base(options, logger, encoder)
        {
            _partnerRepository = partnerRepository;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if(!Request.Headers.TryGetValue(ApiKeyAuthConstants.HeaderName, out var providedKey) || string.IsNullOrWhiteSpace(providedKey))
            {
                return AuthenticateResult.Fail($"Missing {ApiKeyAuthConstants.HeaderName} header.");
            }

            var partner = await ValidateApiKeyAsync(providedKey!);
            if (partner is null)
            {
                return AuthenticateResult.Fail("Invalid API Key");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, partner.PartnerId),
                new Claim("partner_name", partner.Name)
            };

            var identity = new ClaimsIdentity(claims, ApiKeyAuthConstants.SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, ApiKeyAuthConstants.SchemeName);

            return AuthenticateResult.Success(ticket);

        }

        private async Task<ValidatedPartner?> ValidateApiKeyAsync(string apiKey)
        {
            var hashedKey = ApiKeyHasher.Hash(apiKey);
            var key = await _partnerRepository.FindByHashedKeyAsync(hashedKey);

            if (key is null || key.Status == PartnerKeyStatus.Revoked)
            {
                return null;
            }

            if (key.Status == PartnerKeyStatus.Rotating)
            {
                var graceExpiry = key.RotatingSince!.Value.Add(_rotationGracePeriod);
                if (DateTime.UtcNow > graceExpiry)
                {
                    // if grace period elapsed, treat the same as Revoked
                    return null;
                }
            }

            var partner = await _partnerRepository.GetPartnerAsync(key.PartnerId);
            return partner is null ? null : new ValidatedPartner(partner.PartnerId.ToString(), partner.Name);
        }
    }
}
