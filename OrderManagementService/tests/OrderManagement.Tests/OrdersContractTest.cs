using System.Net;
using System.Text.Json;
using OrderManagement.Tests.Infrastructure;

namespace OrderManagement.Tests
{
    // Contract tests - run against a real in-memory host (WebApplicationFactory), exercising the
    // actual middleware pipeline (auth, rate limiting, validation) rather than mocking any of it.
    // The host shares one class fixture, so it's built once for all three tests, and it talks to
    // its own throwaway database: anything these tests provision or seed never touches the dev DB.
    public class OrdersContractTest : IClassFixture<ContractTestFixture>
    {
        private readonly ContractTestFixture _fixture;

        public OrdersContractTest(ContractTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task PartnerOverRateLimit_Returns429_AdminRemainsUnaffected()
        {
            // Arrange
            var adminClient = _fixture.CreateAdminClient();
            var partnerClient = await _fixture.CreatePartnerClientAsync();
            var ordersUrl = $"/api/v1/orders?customerId={TestDatabase.SeededCustomerId}&status=Completed";

            // Act
            // exhaust this partner's (fixture-scoped) permit limit
            for (int i = 0; i < ContractTestFixture.PartnerPermitLimit; i++)
            {
                var response = await partnerClient.GetAsync(ordersUrl);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            var throttledResponse = await partnerClient.GetAsync(ordersUrl);

            // Assert
            // partner gets throttled with a Retry-After header
            Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
            Assert.True(throttledResponse.Headers.Contains("Retry-After"));

            // Admin is a separate, exempt rate-limit partition - unaffected while the partner is throttled
            for (int i = 0; i < 5; i++)
            {
                var adminResponse = await adminClient.GetAsync(ordersUrl);
                Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
            }
        }

        [Fact]
        public async Task GetOrders_V1ResponseShape_Unchanged()
        {
            // Arrange
            var adminClient = _fixture.CreateAdminClient();

            // Act
            var response = await adminClient.GetAsync($"/api/v1/orders?customerId={TestDatabase.SeededCustomerId}&status=Completed");
            response.EnsureSuccessStatusCode();

            // Assert
            // Deserializing into OrderV1Response alone would not catch an accidentally added field
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            // The fixture seeds exactly this many Completed orders for the customer, so there is
            // more than one element to check
            Assert.Equal(TestDatabase.SeededCompletedOrderCount, doc.RootElement.GetArrayLength());

            var expectedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "orderId", "customerId", "total", "status"
            };

            // The shape contract applies to the resource, not just its first item
            foreach (var order in doc.RootElement.EnumerateArray())
            {
                var actualFields = order.EnumerateObject()
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                Assert.Equal(expectedFields, actualFields);
            }
        }

        [Fact]
        public async Task GetOrders_MalformedStatus_ReturnsWithUsefulError()
        {
            // Arrange
            var adminClient = _fixture.CreateAdminClient();

            // Act
            // status isn't a defined OrderStatus value
            var response = await adminClient.GetAsync($"/api/v1/orders?customerId={TestDatabase.SeededCustomerId}&status=NotRealStatus");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
            Assert.False(string.IsNullOrWhiteSpace(errorProp.GetString()));
        }
    }
}