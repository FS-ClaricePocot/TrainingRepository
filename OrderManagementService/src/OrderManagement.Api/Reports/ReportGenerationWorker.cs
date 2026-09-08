namespace OrderManagement.Api.Reports
{
    public class ReportGenerationWorker: BackgroundService
    {
        private readonly ReportJobQueue _queue;
        private readonly ReportService _reportService;
        private readonly ILogger<ReportGenerationWorker> _logger;

        public ReportGenerationWorker(ReportJobQueue queue, ReportService reportService, ILogger<ReportGenerationWorker> logger)
        {
            _queue = queue;
            _reportService = reportService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await foreach(var job in _queue.DequeueAllAsync(cancellationToken))
            {
                try
                {
                    await _reportService.ProcessReportAsync(job.JobId, job.GroupBy, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "An unhandled error occured while processing report job {job.JobId}", job.JobId);
                }
            }    
           
        }
    }
}
