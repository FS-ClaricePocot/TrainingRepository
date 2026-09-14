namespace OrderManagement.Api.Dtos
{
    public class ReportStatusResponse
    {
        public Guid JobId { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<ReportGroupResult>? Result { get; set; }
        public string? Error { get; set; }
    }
}
