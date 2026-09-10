namespace OrderManagement.Api.Dtos
{
    public class OrderV1Response
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
