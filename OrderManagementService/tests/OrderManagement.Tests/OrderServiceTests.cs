using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderManagement.Tests
{
    public class OrderServiceTests
    {
        private const string _unusedConnectionString = "Server=localhost;Database=OrderManagement;User Id=admin;Password=Password;";

        [Fact]
        public async Task GetOrdersForCustomerAsync_WhenCacheAlreadyPopulated_ReturnsCachedValueWithoutDbCall()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new OrderService(_unusedConnectionString, cache);

            const int customerId = 1;
            const string status = "Completed";
            var cachedOrders = new List<Order>
            {
                new Order { OrderId = 1, CustomerId = customerId, Total = 150m, Status = status }

            };

            cache.Set(
                $"{customerId}:{status}",
                new Lazy<Task<List<Order>>>(() => Task.FromResult(cachedOrders)));

            // Act
            var result = await service.GetOrdersForCustomerAsync(customerId, status);

            // Assert
            Assert.Single(result);
            Assert.Equal(1, result[0].OrderId);
        }
    }
}
