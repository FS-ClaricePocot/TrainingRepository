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
        private static ReportsV1Controller BuildController(out IMemoryCache jobstore)
        {
            jobstore = new MemoryCache(new MemoryCacheOptions());
            var reportService = new ReportService("unused", jobstore, new ReportJobQueue());
            var logger = new Mock<ILogger<ReportsV1Controller>>().Object;
            return new ReportsV1Controller(reportService, logger);
        }

        [Fact]
        public async Task GenerateReport_ValidGroupBy_HandsOffAndReturns202WithJobHandle()
        {
            // Arrange
            var controller = BuildController(out var jobStore);

            // Act
            var result = await controller.GenerateReport(new GenerateReportRequest("ByStatus"));

            // Assert - 202, a real job handle, and the job handle registered
            var accepted = Assert.IsType<AcceptedAtActionResult>(result);
            var body = Assert.IsType<ReportJobAcceptedResponse>(accepted.Value);
            Assert.NotEqual(Guid.Empty, body.JobId);
            Assert.True(jobStore.TryGetValue(body.JobId, out _));
        }

        [Fact]
        public async Task GenerateReport_InvalidGroupby_Returns400()
        {
            // Arrange
            var controller = BuildController(out _);

            //Act
            var result = await controller.GenerateReport(new GenerateReportRequest("InvalidGrouping"));

            //Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
