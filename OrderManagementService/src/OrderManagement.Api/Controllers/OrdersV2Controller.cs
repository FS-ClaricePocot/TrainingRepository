using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Validation;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/v2/orders")]
    public class OrdersV2Controller : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly ILogger<OrdersV2Controller> _logger;

        public OrdersV2Controller(OrderService orderService, ILogger<OrdersV2Controller> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int customerId, [FromQuery] string status)
        {
            if (!OrdersQueryValidator.IsValid(customerId, status))
            {
                return BadRequest(new { error = "Missing/Invalid customerId or status" });
            }

            try
            {
                var orders = await _orderService.GetOrdersForCustomerAsync(customerId, status);

                var response = orders.Select(o => new OrderV2Response
                {
                    OrderId = o.OrderId,
                    CustomerId = o.CustomerId,
                    Total = o.Total,
                    Status = o.Status,
                    // TODO: populate real v2-only fields once scoped.
                }).ToList();

                return Ok(response);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failure while retrieving orders for {customerId}", customerId);
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }
    }
}
