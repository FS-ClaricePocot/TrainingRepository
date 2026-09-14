namespace OrderManagement.Api.Dtos
{
    public class ProvisionPartnerResponse
    {
        public int PartnerId { get; set; }
        public string Name { get; set; } = String.Empty;
        public string ApiKey { get; set; } = String.Empty;
    }
}
