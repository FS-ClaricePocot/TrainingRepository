using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/v2/orders")]
    public class OrdersV2Controller : ControllerBase
    {
        private readonly OrderService _orderService;

        public OrdersV2Controller(OrderService orderService)
        {
            _orderService = orderService;
        }

        //TO DO: scaffolding only; update implementation after design doc feedback
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int customerId, [FromQuery] string status)
        {
            if (customerId <= 0 || string.IsNullOrEmpty(status))
            {
                return BadRequest(new { error = "Missing/Invalid customerId or status" });
            }

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
    }
}
