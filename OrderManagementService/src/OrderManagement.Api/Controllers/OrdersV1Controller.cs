using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/v1/orders")]
    public class OrdersV1Controller : ControllerBase
    {
        private readonly OrderService _orderService;

        public OrdersV1Controller(OrderService orderService)
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

            var response = orders.Select(o => new OrderV1Response
            {
                OrderId = o.OrderId,
                CustomerId = o.CustomerId,
                Total = o.Total,
                Status = o.Status
            }).ToList();

            return Ok(response);
        }

    }
}
