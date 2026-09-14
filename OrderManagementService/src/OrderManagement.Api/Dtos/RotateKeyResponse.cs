namespace OrderManagement.Api.Dtos
{
    public class RotateKeyResponse
    {
        public int PartnerId { get; set; }
        public int NewKeyId { get; set; }
        public string ApiKey { get; set; } = String.Empty;
    }
}
