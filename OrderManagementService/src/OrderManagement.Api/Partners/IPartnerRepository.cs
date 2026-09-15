namespace OrderManagement.Api.Partners
{
    public interface IPartnerRepository
    {
        Task<Partner> CreatePartnerAsync(string name);
        Task<PartnerApiKey> CreateKeyAsync(int partnerId, string hashedKey, PartnerKeyStatus status);
        Task<PartnerApiKey?> FindByHashedKeyAsync(string hashedKey);
        Task<PartnerApiKey?> GetActiveKeyForPartnerAsync(int partnerId);
        Task MarkRotatingAsync(int keyId);
        Task MarkRevokedAsync(int keyId);
        Task<Partner?> GetPartnerAsync(int partnerId);
    }

}
