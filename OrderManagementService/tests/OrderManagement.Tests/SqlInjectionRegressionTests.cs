using OrderManagement.Core.Repositories;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text;
using Microsoft.OpenApi;

namespace OrderManagement.Tests
{
    public class SqlInjectionRegressionTests
    {
        // Local dev connection string
        private const string connectionString = "Server=localhost;Database=OrderManagementDb;Trusted_Connection=True;TrustServerCertificate=True;";

        [Theory]
        [InlineData("Completed'; DROP TABLE Orders; --")]
        [InlineData("' OR '1' = '1")]
        [InlineData("Completed' UNION SELECT CustomerId, Name, Email, IsActive FROM Customers--")]
        public async Task LoadOrdersAsync_InjectionPayloadInStatus_TreatedAsLiteralValue(string payload)
        {
            // Arrange
            var repository = new SqlOrderRepository(connectionString);

            // Act
            // the payload becomes value for @status parameter
            var result = await repository.LoadOrdersAsync(1,payload);

            //Asert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Orders_SurviveInjectionAttempts_TableAndSeedDataIntact()
        {
            // Arrange
            var repository = new SqlOrderRepository(connectionString);
            string query = "SELECT COUNT(*) FROM Orders";

            // Act
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            var orderCount = (int)(await cmd.ExecuteScalarAsync())!;

            // Assert
            Assert.True(orderCount > 0);
            // check known data is untouched
            var completedOrders = await repository.LoadOrdersAsync(1, "Completed");
            Assert.Equal(2, completedOrders.Count);

        }
    }
}
