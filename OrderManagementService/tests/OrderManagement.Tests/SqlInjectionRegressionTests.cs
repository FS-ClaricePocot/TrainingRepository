using OrderManagement.Core.Repositories;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text;
using Microsoft.OpenApi;
using OrderManagement.Tests.Infrastructure;

namespace OrderManagement.Tests
{
    public class SqlInjectionRegressionTests : IClassFixture<TestDatabase>
    {
        private static readonly string[] InjectionPayloads =
        {
            "Completed'; DROP TABLE Orders; --",
            "' OR '1' = '1",
            "Completed' UNION SELECT CustomerId, Name, Email, IsActive FROM Customers--"
        };

        public static IEnumerable<object[]> Payloads => InjectionPayloads.Select(p => new object[] { p });

        private readonly TestDatabase _db;
        public SqlInjectionRegressionTests(TestDatabase db)
        {
            _db = db;
        }

        [Theory]
        [MemberData(nameof(Payloads))]
        public async Task LoadOrdersAsync_InjectionPayloadInStatus_TreatedAsLiteralValue(string payload)
        {
            // Arrange
            var repository = new SqlOrderRepository(_db.ConnectionString);

            // Act
            // the payload becomes value for @status parameter
            var result = await repository.LoadOrdersAsync(1, payload);

            //Asert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Orders_SurviveInjectionAttempts_TableAndSeedDataIntact()
        {
            // Arrange
            var repository = new SqlOrderRepository(_db.ConnectionString);

            // Act
            foreach (var payload in InjectionPayloads)
            {
                await repository.LoadOrdersAsync(TestDatabase.SeededCustomerId, payload);
            }

            // Assert
            // The table still exists and holds exactly the seeded rows
            var orderCount = await _db.ScalarAsync<int>("SELECT COUNT(*) FROM Orders");
            Assert.Equal(TestDatabase.SeededTotalOrderCount, orderCount);

            var completedOrders = await repository.LoadOrdersAsync(TestDatabase.SeededCustomerId, "Completed");
            Assert.Equal(TestDatabase.SeededCompletedOrderCount, completedOrders.Count);

        }
    }
}
