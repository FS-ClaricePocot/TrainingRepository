using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Reports;
using OrderManagement.Api.Validation;
using OrderManagement.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using OrderManagement.Api.Auth;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/v1/reports")]
    [EnableRateLimiting(RateLimitPolicyNames.OrdersApi)]
    [Authorize(Policy = AuthorizationPolicyNames.DualConsumer)]
    public class ReportsV1Controller : ControllerBase
    {
        private readonly ReportService _reportService;
        private readonly ILogger<ReportsV1Controller> _logger;

        public ReportsV1Controller(ReportService reportService, ILogger<ReportsV1Controller> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport([FromBody] GenerateReportRequest request)
        {
            if (!ReportsValidator.IsValidGroupBy(request.GroupBy))
            {
                return BadRequest("Missing/Invalid groupBy");
            }

            try
            {
                var jobId = await _reportService.QueueReportAsync(request.GroupBy);
                return AcceptedAtAction(nameof(GetReportStatus), new { jobId }, new ReportJobAcceptedResponse { JobId = jobId });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failure while queueing report generation");
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }

        [HttpGet("{jobId}")]
        public IActionResult GetReportStatus(Guid jobId)
        {
            var job = _reportService.GetJob(jobId);
            if (job is null)
            {
                return NotFound(new { error = $"Report job {jobId} was not found" });
            }

            return Ok(new ReportStatusResponse
            {
                JobId = jobId,
                Status = job.Status.ToString(),
                Result = job.Result,
                Error = job.Error
            });
        }
    }
}
