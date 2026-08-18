using Microsoft.AspNetCore.Mvc;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly OrderService _orderService;

        public OrdersController(OrderService orderService)
        {
            _orderService = orderService;
        }

        //GET api/orders/?customerId=1&status="Completed"
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int customerId, [FromQuery] string status)
        {
            if(customerId <= 0 || string.IsNullOrEmpty(status))
            {
                return BadRequest(new { error = "Missing/Invalid customerId or status" });
            }

            var orders = await _orderService.GetOrdersForCustomerAsync(customerId, status);

            return Ok(orders);
        }

    }
}
