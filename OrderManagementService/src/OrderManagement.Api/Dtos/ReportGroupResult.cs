namespace OrderManagement.Api.Dtos
{
    // Shared shape for both ByStatus, ByCustomers
    // Only the fields relevant to the requested GroupBy is populated
    public class ReportGroupResult
    {
        public string? Status { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalSum { get; set; }
       
    }
}
