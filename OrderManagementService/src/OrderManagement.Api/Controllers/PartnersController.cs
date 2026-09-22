using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Auth;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Partners;

namespace OrderManagement.Api.Controllers
{
    // Admin-only - keys are admin - generated, not self-service, per the
    // design doc. Deliberately no [EnableRateLimiting] - this isn't part
    // of the dual-consumer Orders surface, and it's already gated to
    // InternalAdmin only.
    [ApiController]
    [Route("api/v1/partners")]
    [Authorize(Policy = AuthorizationPolicyNames.InternalAdmin)]
    public class PartnersController: ControllerBase
    {
        private readonly IPartnerRepository _partnerRepository;
        private readonly ILogger<PartnersController> _logger;

        public PartnersController(IPartnerRepository partner, ILogger<PartnersController> logger)
        {
            _partnerRepository = partner;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ProvisionPartner([FromBody] ProvisionPartnerRequest request)
        {
            if(string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Missing/Invalid name" });
            }

            try
            {
                var partner = await _partnerRepository.CreatePartnerAsync(request.Name);
                var rawKey = ApiKeyHasher.GenerateRawKey();
                var hashedKey = ApiKeyHasher.Hash(rawKey);

                await _partnerRepository.CreateKeyAsync(partner.PartnerId, hashedKey, PartnerKeyStatus.Active);

                return Ok(new ProvisionPartnerResponse
                {
                    PartnerId = partner.PartnerId,
                    Name = partner.Name,
                    ApiKey = rawKey
                });
            }
            catch(Exception exception)
            {
                _logger.LogError(exception, "Failed while provisioning partner {name}", request.Name);
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }

        [HttpPost("{partnerId}/rotate-key")]
        public async Task<IActionResult> RotateKey(int partnerId)
        {
            if (partnerId <= 0)
            {
                return BadRequest(new { error = "Missing/Invalid partnerId" });
            }

            try
            {
                var partner = await _partnerRepository.GetPartnerAsync(partnerId);
                if(partner is null)
                {
                    return NotFound(new { error = $"Partner {partnerId} was not found." });
                }

                var currentKey = await _partnerRepository.GetActiveKeyForPartnerAsync(partnerId);
                if (currentKey is not null)
                {
                    await _partnerRepository.MarkRotatingAsync(currentKey.KeyId);
                }

                var rawKey = ApiKeyHasher.GenerateRawKey();
                var hashedKey = ApiKeyHasher.Hash(rawKey);
                var newKey = await _partnerRepository.CreateKeyAsync(partnerId, hashedKey, PartnerKeyStatus.Active);

                return Ok(new RotateKeyResponse
                {
                    PartnerId = partnerId,
                    NewKeyId = newKey.KeyId,
                    ApiKey = rawKey
                });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failure while rotating key for partner {partnerId}", partnerId);
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }

        [HttpPost("{partnerId}/keys/{keyId}/revoke")]
        public async Task<IActionResult> RevokeKey(int partnerId, int keyId)
        {
            if (partnerId <= 0 || keyId <=0)
            {
                return BadRequest(new { error = "Missing/Invalid partnerId or keyId" });
            }

            try
            {
                var revoked = await _partnerRepository.MarkRevokedAsync(partnerId: partnerId, keyId: keyId);
                if(!revoked)
                {
                    return NotFound(new { error = $"Key {keyId} was not found for partner {partnerId}." });
                }

                return NoContent();
            }
            catch(Exception exception)
            {
                _logger.LogError(exception, "Failure while revoking key {keyId} for partner {partner}", keyId, partnerId);
                return StatusCode(500, new { error = "An internal error occurred" });
            }
        }
    }
}
