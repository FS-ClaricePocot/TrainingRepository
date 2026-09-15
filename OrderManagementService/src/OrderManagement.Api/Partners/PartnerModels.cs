namespace OrderManagement.Api.Partners
{
    public enum PartnerKeyStatus
    {
        Active, 
        Rotating, 
        Revoked
    }

    public class Partner
    {
        public int PartnerId { get; set; }
        public string Name { get; set; } = String.Empty;
    }

    public class PartnerApiKey
    {
        public int KeyId { get; set; }
        public int PartnerId { get; set; }
        public string HashedKey { get; set; } = String.Empty;
        public PartnerKeyStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RotatingSince { get; set; }
        public DateTime? RevokedAt { get; set; }

    }

    // What a succesful validation resolves to
    public record ValidatedPartner(string PartnerId, string Name);
}
