using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using OrderManagement.Api.Controllers;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Reports;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderManagement.Tests
{
    public class ReportsControllerTests
    {
        private static ReportsV1Controller BuildController(Mock<IReportService> reportService)
        {
            var logger = new Mock<ILogger<ReportsV1Controller>>().Object;
            return new ReportsV1Controller(reportService.Object, logger);
        }

        [Fact]
        public async Task GenerateReport_ValidGroupBy_HandsOffAndReturns202WithJobHandle()
        {
            // Arrange
            var reportService = new Mock<IReportService>(MockBehavior.Strict);
            var jobId = Guid.NewGuid();
            reportService.Setup(s => s.QueueReportAsync("ByStatus")).ReturnsAsync(jobId);
            var controller = BuildController(reportService);

            // Act
            var result = await controller.GenerateReport(new GenerateReportRequest("ByStatus"));

            // Assert - 202, a real job handle, and the job handle registered
            var accepted = Assert.IsType<AcceptedAtActionResult>(result);
            var body = Assert.IsType<ReportJobAcceptedResponse>(accepted.Value);
            Assert.Equal(jobId, body.JobId);
        }

        [Fact]
        public async Task GenerateReport_InvalidGroupby_Returns400()
        {
            // Arrange
            var reportService = new Mock<IReportService>(MockBehavior.Strict);
            var controller = BuildController(reportService);

            //Act
            var result = await controller.GenerateReport(new GenerateReportRequest("InvalidGrouping"));

            //Assert
            Assert.IsType<BadRequestObjectResult>(result);
            // Validation fails vefore QueueReportAsync is ever reached
            reportService.Verify(s => s.QueueReportAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GenerateReport_ServiceThrows_Returns500()
        {
            // Arrage 
            var reportService = new Mock<IReportService>(MockBehavior.Strict);
            reportService.Setup(s => s.QueueReportAsync("ByStatus"))
                .ThrowsAsync(new InvalidOperationException("simulated failure"));
            var controller = BuildController(reportService);

            // Act
            var result = await controller.GenerateReport(new GenerateReportRequest("ByStatus"));

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetReportStatus_JobExists_ReturnsOkWithStatus()
        {
            // Arrange
            var reportService = new Mock<IReportService>(MockBehavior.Strict);
            var jobId = Guid.NewGuid();
            var record = new ReportJobRecord { Status = ReportJobStatus.Completed };
            reportService.Setup(s => s.GetJob(jobId)).Returns(record);
            var controller = BuildController(reportService);

            // Act
            var result = controller.GetReportStatus(jobId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<ReportStatusResponse>(ok.Value);
            Assert.Equal(jobId, body.JobId);
            Assert.Equal("Completed", body.Status);
        }

        [Fact]
        public async Task GetReportStatus_JobNotFound_Returns404()
        {
            // Arange 
            var reportService = new Mock<IReportService>(MockBehavior.Strict);
            var jobId = Guid.NewGuid();
            reportService.Setup(s => s.GetJob(jobId)).Returns((ReportJobRecord?)null);
            var controller = BuildController(reportService);

            // Act
            var result = controller.GetReportStatus(jobId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }

    
}
