namespace OrderManagement.Api.Dtos
{
    // v1 response contract. Per the design doc's compatibility guarantee,
    // this shape is permanent -- fields are never removed or renamed once
    // v1 ships. v2 only ever adds to it (see OrderV2Response).
    public class OrderV1Response
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
