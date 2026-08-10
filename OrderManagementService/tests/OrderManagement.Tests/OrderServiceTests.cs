using Microsoft.SqlServer.Server;
using Moq;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.NetworkInformation;
using System.Text;

namespace OrderManagement.Tests
{
    //At least 4 tests covering normal behavior(happy paths).
    //At least 2 tests for edge cases(null inputs, empty results, invalid states).
    //At least 1 test that would catch a regression — something that could break silently if someone changes the code later.

    

    //helpers
    public class OrderServiceTests
    {
        public static Order MakeOrder(int orderId, int customerId, decimal total, string status)
        {
            return new Order
            {
                OrderId = orderId,
                CustomerId = customerId,
                Total = total,
                Status = status

            };
        }

        private const string _unusedConnectionString = "Server=localhost;Database=OrderManagement;User Id=admin;Password=Password;";

        private static Mock<IDbConnectionFactory> CreateMockDbFactory(List<Order> rows)
        {
            int rowIndex = -1;

            var mockReader = new Mock<IDataReader>();
            mockReader.Setup(r => r.Read()).Returns(() =>
            {
                rowIndex++;
                return rowIndex < rows.Count;
            });

            mockReader.Setup(r => r.GetInt32(0)).Returns(() => rows[rowIndex].OrderId);
            mockReader.Setup(r => r.GetInt32(1)).Returns(() => rows[rowIndex].CustomerId);
            mockReader.Setup(r => r.GetDecimal(2)).Returns(() => rows[rowIndex].Total);
            mockReader.Setup(r => r.GetString(3)).Returns(() => rows[rowIndex].Status);

            var mockParameters = new Mock<IDataParameterCollection>();
            mockParameters.Setup(p => p.Add(It.IsAny<object>())).Returns(0);

            var mockCommand = new Mock<IDbCommand>();
            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.Setup(c => c.CreateParameter()).Returns(() => new Mock<IDbDataParameter>().Object);
            mockCommand.Setup(c => c.Parameters).Returns(mockParameters.Object);
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);
            mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(1);

            var mockConnection = new Mock<IDbConnection>();
            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(mockConnection.Object);

            return mockFactory;
        }

        //Happy Path
        //1. GetOrderForCustomer_WithValidCustomerIdAndStatus_ReturnsOrdersFromCache
        [Fact]
        public void GetOrderForCustomer_WithValidCustomerIdAndStatus_ReturnsOrdersFromCache()
        {
            //Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            var orderService = new OrderService(_unusedConnectionString, cache);
            int customerId = 1;
            string status = "Completed";
            var order = new List<Order>
            {
                MakeOrder(1, customerId, 100m, status)
            };
            cache[(customerId, status)] = order;

            //Act
            var orders = orderService.GetOrderForCustomer(customerId, status);

            //Assert
            Assert.NotNull(orders);
            Assert.All(orders, o => Assert.Equal(customerId, o.CustomerId));
            Assert.All(orders, o => Assert.Equal(status, o.Status));
        }

        //2. GetOrderForCustomer_WithValidCustomerIdAndStatus_ReturnsOrdresFromDatabase
        [Fact]
        public void GetOrderForCustomer_WithValidCustomerIdAndStatus_ReturnsOrdresFromDatabase()
        {

            // Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            int customerId = 1;
            string status = "Completed";
            var expectedOrder = MakeOrder(1, customerId, 100m, status);
            var mockFactory = CreateMockDbFactory(new List<Order> { expectedOrder });
            var orderService = new OrderService(_unusedConnectionString, cache, mockFactory.Object);

            //Act
            var ordersRetrieved = orderService.GetOrderForCustomer(customerId, status);

            //Assert 
            Assert.NotNull(ordersRetrieved);
            Assert.Equal(expectedOrder.OrderId, ordersRetrieved[0].OrderId);
            Assert.Equal(expectedOrder.CustomerId, ordersRetrieved[0].CustomerId);
            Assert.Equal(expectedOrder.Total, ordersRetrieved[0].Total);
            Assert.Equal(expectedOrder.Status, ordersRetrieved[0].Status);

        }

        //3. GetOrderForCustomer_WithMultipleOrders_ReturnsAllOrders
        [Fact]
        public void GetOrderForCustomer_WithMultipleOrder_ReturnsAllOrders()
        {
            // Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            int customerId = 1;
            string status = "Completed";
            var expectedOrder = new List<Order>
            {
                MakeOrder(1, customerId, 100m, status),
                MakeOrder(2, customerId, 100m, status)
            };
            var mockFactory = CreateMockDbFactory(expectedOrder);
            var orderService = new OrderService(_unusedConnectionString, cache, mockFactory.Object);

            //Act
            var ordersRetrieved = orderService.GetOrderForCustomer(customerId, status);

            //Assert
            Assert.Equal(expectedOrder.Count, ordersRetrieved.Count);
            Assert.Equal(expectedOrder[0].OrderId, ordersRetrieved[0].OrderId);
            Assert.Equal(expectedOrder[1].OrderId, ordersRetrieved[1].OrderId);

        }

        //4. GetOrderForCustomer_WithNoOrders_ReturnsEmptyList
        [Fact]
        public void GetOrderForCustomer_WithNoOrders_ReturnsEmptyList()
        {
            // Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            int customerId = 999; // customer with no order
            string status = "Completed";
            var mockFactory = CreateMockDbFactory(new List<Order>());
            var orderService = new OrderService(_unusedConnectionString, cache, mockFactory.Object);

            //Act
            var ordersRetrieved = orderService.GetOrderForCustomer(customerId, status);

            //Assert
            Assert.Empty(ordersRetrieved);
        }


        //5. GetTotalSpend_WithValidCustomerId_ReturnsTotalSpend
        public void GetTotalSpend_WithValidCustomerId_ReturnsTotalSpend()
        {
            // Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            int customerId = 1;
            string status = "Completed";
            var dbOrders = new List<Order>
            {
                MakeOrder(1, customerId, 100m, status),
                MakeOrder(2, customerId, 100m, status)
            };

            var mockFactory = CreateMockDbFactory(dbOrders);
            var orderService = new OrderService(_unusedConnectionString, cache, mockFactory.Object);

            //Act 
            var totalSpend = orderService.GetTotalSpend(customerId);

            //Assert
            Assert.Equal(200m, totalSpend);


        }

        //Edge Cases
        //1. GetOrderForCustomer_WithNullStatus_ThrowArgumentException
        [Fact]
        public void GetOrderForCustomer_WithNullStatus_ThrowArgumentException()
        {
            // Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            var orderService = new OrderService(_unusedConnectionString, cache);
            int customerId = 1;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => orderService.GetOrderForCustomer(customerId, null));
            Assert.Equal("status", ex.ParamName);

        }

        //2. UpdateOrderStatus_WithInvalidOrderId_ThrwoException
        [Fact]
        public void UpdateOrderStatus_WithInvalidOrderId_ThrowsException()
        {
            // Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            var orderService = new OrderService(_unusedConnectionString, cache);
            int invalidOrderId = -1;

            //Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orderService.UpdateOrderStatus(invalidOrderId, "Completed"));
            Assert.Equal("orderId", ex.ParamName);
        }

        // Regression
        // 1. GetTotalSpend_WithDifferentStatuses_ReturnsCorrectTotalSpend
        [Fact]
        public void GetTotalSpend_WithDifferentStatuses_ReturnsCorrectTotalSpend()
        {
            // Arrange
            var cache = new Dictionary<(int, string), List<Order>>();
            int customerId = 1;
            cache[(customerId, "Pending")] = new List<Order>
            {
                MakeOrder(3, customerId, 900m, "Pending")
            };

            var completedOrders = new List<Order>
            {
                MakeOrder(1, customerId, 100m, "Completed"),
                MakeOrder(2, customerId, 25m, "Completed")
            };

            var mockFactory = CreateMockDbFactory(completedOrders);
            var orderService = new OrderService(_unusedConnectionString, cache, mockFactory.Object);

            // Act
            var totalSpend = orderService.GetTotalSpend(customerId);

            // Assert
            Assert.Equal(125m, totalSpend);

        }

    }

}

