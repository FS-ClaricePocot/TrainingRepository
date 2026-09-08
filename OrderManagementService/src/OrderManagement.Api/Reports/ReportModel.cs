using OrderManagement.Api.Dtos;

namespace OrderManagement.Api.Reports
{
    public record ReportJobRequest(Guid JobId, string GroupBy);
    public class ReportJobRecord
    {
        public ReportJobStatus Status { get; set; }
        public List<ReportGroupResult>? Result { get; set; }
        public string? Error { get; set; }


    }
}
