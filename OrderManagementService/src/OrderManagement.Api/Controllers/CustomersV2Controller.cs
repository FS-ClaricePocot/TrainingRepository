using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/v2/customers")]
    public class CustomersV2Controller : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly ILogger<CustomersV2Controller> _logger;

        public CustomersV2Controller(OrderService orderService, ILogger<CustomersV2Controller> logger)
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
                return Ok(new TotalSpendV2Response { CustomerId = customerId, TotalSpend = totalSpend });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failure while retrieving total spend for {customerId}", customerId);
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }
    }
}
