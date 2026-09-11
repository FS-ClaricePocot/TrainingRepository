using Microsoft.AspNetCore.Mvc;
using OrderManagement.Core.Enums;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly ILogger _logger;

        public OrdersController(OrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        //GET api/orders/?customerId=1&status="Completed"
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int customerId, [FromQuery] string status)
        {
            if (customerId <= 0 || string.IsNullOrEmpty(status) || !Enum.IsDefined(typeof(OrderStatus), status))
            {
                return BadRequest(new { error = "Missing/Invalid customerId or status" });
            }

            try
            {
                var orders = await _orderService.GetOrdersForCustomerAsync(customerId, status);
                return Ok(orders);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failure while retrieving orders for {customerId}", customerId);
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }

    }
}
