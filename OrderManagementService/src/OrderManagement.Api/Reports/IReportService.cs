namespace OrderManagement.Api.Reports
{
    public interface IReportService
    {
        Task<Guid?> QueueReportAsync(string groupBy);
        ReportJobRecord? GetJob(Guid jobId);
    }
}
