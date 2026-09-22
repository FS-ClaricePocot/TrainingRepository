using Microsoft.Extensions.Caching.Memory;
using Moq;
using OrderManagement.Core.Repositories;

namespace OrderManagement.Tests
{
    public class OrderServiceTests
    {

        [Fact]
        public async Task GetOrdersForCustomerAsync_WhenCacheAlreadyPopulated_ReturnsCachedValueWithoutDbCall()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            var service = new OrderService(repo.Object, cache);

            const int customerId = 1;
            const string status = "Completed";
            var cachedOrders = new List<Order>
            {
                new Order { OrderId = 1, CustomerId = customerId, Total = 150m, Status = status }

            };

            cache.Set(
                $"{customerId}:{status}",
                cachedOrders);

            // Act
            var result = await service.GetOrdersForCustomerAsync(customerId, status);

            // Assert
            Assert.Single(result);
            Assert.Equal(1, result[0].OrderId);
            //Makes sure that values are truly retrieved from cache and no calls are made to the DB
            repo.Verify(r => r.LoadOrdersAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
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
        public async Task GetOrdersForCustomerAsync_ConcurrentCallsForSameKey_OnlyCallsRepoOnce()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            var callCount = 0;

            repo.Setup(r => r.LoadOrdersAsync(1, "Completed"))
                .Returns(async () =>
                {
                    Interlocked.Increment(ref callCount);
                    await Task.Delay(50); // simulate DB latency so calls overlap
                    return new List<Order>
                    {
                        new() { OrderId = 1, CustomerId = 1, Total = 100m, Status = "Completed" }
                    };
                });

            var service = new OrderService(repo.Object, cache);

            // Act
            // Fire 20 concurrent requests for the exact same key.
            var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() => service.GetOrdersForCustomerAsync(1, "Completed")));
            var results = await Task.WhenAll(tasks);

            // Assert
            // Every caller should get a result but the repo should only be hit once
            Assert.All(results, r => Assert.Single(r));
            Assert.Equal(1, callCount);
            repo.Verify(r => r.LoadOrdersAsync(1, "Completed"), Times.Once);

        }

        [Fact]
        public async Task GetOrdersForCustomerAsync_WhenRepoThrows_RemovesCacheEntrySoNextCallRetries()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            var goodOrders = new List<Order> { new() { OrderId = 1, CustomerId = 1, Total = 100m, Status = "Completed" } };

            repo.SetupSequence(r => r.LoadOrdersAsync(1, "Completed"))
                .ThrowsAsync(new InvalidOperationException("simulated DB failure"))
                .ReturnsAsync(goodOrders);

            var service = new OrderService(repo.Object, cache);

            // Act & Assert
            // first call fails and throws exception
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetOrdersForCustomerAsync(1, "Completed"));

            // If the failed entry was not removed, this would throw the idential exception
            // instead of retrying against the repo
            var result = await service.GetOrdersForCustomerAsync(1, "Completed");

            Assert.Single(result);
            repo.Verify(r => r.LoadOrdersAsync(1, "Completed"), Times.Exactly(2));
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
        public async Task GetTotalSpendAsync_MixedOrders_SumCompletedOrdersOnly()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
            var mixedOrders = new List<Order>
            {
                new() { OrderId = 1, CustomerId = 1, Total = 150.00m, Status = "Completed"},
                new() { OrderId = 2, CustomerId = 1, Total = 25.50m, Status = "Completed"},
                new() { OrderId = 3, CustomerId = 1, Total = 100.00m, Status = "Pending"}
            };

            repo.Setup(r => r.LoadOrdersAsync(1, "Completed")).ReturnsAsync(mixedOrders);
            var service = new OrderService(repo.Object, cache);

            //Act
            var total = await service.GetTotalSpendAsync(1);

            // Assert
            // total should only be for orders with "Completed" status
            Assert.Equal(175.50m, total);
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
