using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Moq;
using OrderManagement.Core.Repositories;
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
            var service = new OrderService(Mock.Of<IOrderRepository>(), cache);

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

        [Fact]
        public async Task GetOrdersForCustomerAsync_CachedUnderOneStatus_DoesNotLeakIntoAnotherStatusKey()
        {
            // Arrange 
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);

            var completedOrders = new List<Order> { new() { OrderId = 1, CustomerId = 1, Total = 100m, Status = "Completed" } };
            var pendingOrders = new List<Order> { new() { OrderId = 2, CustomerId = 1, Total = 40m, Status = "Pending" } };

            repo.Setup(r => r.LoadOrdersAsync(1, "Completed")).ReturnsAsync(completedOrders);
            repo.Setup(r => r.LoadOrdersAsync(1, "Pending")).ReturnsAsync(pendingOrders);

            var service = new OrderService(repo.Object, cache);

            // Act
            var firstCompleted = await service.GetOrdersForCustomerAsync(1, "Completed");
            var firstPending = await service.GetOrdersForCustomerAsync(1, "Pending");
            var secondCompleted = await service.GetOrdersForCustomerAsync(1, "Completed"); // should now be from cache

            // Assert
            Assert.Equal("Completed", Assert.Single(firstCompleted).Status);
            Assert.Equal("Pending", Assert.Single(firstPending).Status);
            Assert.Equal("Completed", Assert.Single(secondCompleted).Status);

            // There should only be one DB call each (customer, status) before they are stored to cache
            // Once stored in the cache, there should be no refetching
            repo.Verify(r => r.LoadOrdersAsync(1, "Completed"), Times.Once);
            repo.Verify(r => r.LoadOrdersAsync(1, "Pending"), Times.Once);

        }

        [Fact]
        public async Task GetTotalSpendAsync_SumsCompletedOrders()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            var completedOrders = new List<Order>
            {
                new() { OrderId = 1, CustomerId = 1, Total = 150.00m, Status = "Completed"},
                new() { OrderId = 2, CustomerId = 1, Total = 25.50m, Status = "Completed" }
            };


            repo.Setup(r => r.LoadOrdersAsync(1, "Completed")).ReturnsAsync(completedOrders);
            var service = new OrderService(repo.Object, cache);

            // Act
            var total = await service.GetTotalSpendAsync(1);

            // Assert
            Assert.Equal(175.50m, total);
            // Verify only with Completed status are totaled, not other statuses
            repo.Verify(r => r.LoadOrdersAsync(1, "Completed"), Times.Once);
        }


        [Fact]
        public async Task GetTotalSpendAsync_NoCompletedOrders_ReturnsZero()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            repo.Setup(r => r.LoadOrdersAsync(1, "Completed")).ReturnsAsync(new List<Order>());
            var service = new OrderService(repo.Object, cache);

            // Act
            var total = await service.GetTotalSpendAsync(1);

            // Assert
            Assert.Equal(0m, total);
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_OrderNotFound_ThrowsAndSkipsUpdate()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            repo.Setup(r => r.GetOrderCustomerAndStatusAsync(999))
                .ReturnsAsync(((int?)null, (string?)null));
            var service = new OrderService(repo.Object, cache);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateOrderStatusAsync(999, "Completed"));

            repo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
           
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_EvictsOnlyAffectedKeys()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            repo.Setup(r => r.GetOrderCustomerAndStatusAsync(3)).ReturnsAsync((1, "Pending"));
            repo.Setup(r => r.UpdateOrderStatusAsync(3, "Completed")).Returns(Task.CompletedTask);
            var service = new OrderService(repo.Object, cache);

            cache.Set("1:Pending", new Object());
            cache.Set("1:Completed", new Object());
            cache.Set("2:Pending", new Object());
            cache.Set("2:Completed", new Object());

            // Act
            await service.UpdateOrderStatusAsync(3, "Completed");

            // Assert
            Assert.False(cache.TryGetValue("1:Pending", out _));
            Assert.False(cache.TryGetValue("1:Completed", out _));
            Assert.True(cache.TryGetValue("2:Pending", out _));
            Assert.True(cache.TryGetValue("2:Completed", out _));
        }
    }
}
