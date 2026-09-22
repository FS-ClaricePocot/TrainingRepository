using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderManagement.Api.Auth;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.RateLimiting;
using OrderManagement.Api.Validation;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/v2/orders")]
    [EnableRateLimiting(RateLimitPolicyNames.OrdersApi)]
    [Authorize(Policy = AuthorizationPolicyNames.DualConsumer)]
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

        [HttpPatch("{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, [FromBody] UpdateOrderStatusRequest request)
        {
            if (orderId <= 0 || !OrdersQueryValidator.IsValidStatus(request.Status))
            {
                return BadRequest(new { error = "Missing/Invalid orderId or status" });
            }

            try
            {
                await _orderService.UpdateOrderStatusAsync(orderId, request.Status);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound(new { error = $"Order {orderId} was not found." });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failure while updating status for order {orderId}", orderId);
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }
    }
}
