using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.RateLimiting;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/v1/customers")]
    [EnableRateLimiting(RateLimitPolicyNames.OrdersApi)]
    public class CustomersV1Controller : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly ILogger<CustomersV1Controller> _logger;

        public CustomersV1Controller(OrderService orderService, ILogger<CustomersV1Controller> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpGet("{customerId}/total-spend")]
        public async Task<IActionResult> GetTotalSpend(int customerId)
        {
            if (customerId <= 0)
            {
                return BadRequest(new { error = "Missing/Invalid customerId" });
            }

            try
            {
                var totalSpend = await _orderService.GetTotalSpendAsync(customerId);
                return Ok(new TotalSpendV1Response { CustomerId = customerId, TotalSpend = totalSpend });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failure while retrieving total spend for {customerId}", customerId);
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }
    }
}
