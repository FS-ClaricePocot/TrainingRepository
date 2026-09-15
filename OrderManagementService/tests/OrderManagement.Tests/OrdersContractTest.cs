using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OrderManagement.Api.Dtos;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OrderManagement.Tests
{

    // Contract tests - run against a real in-memory host (WebApplicationFactory),
    // exercising the actual middleware pipeline (auth, rate limiting, validation) rather
    // than mocking any of it. Shares one host across all three tests via IClassFixture -
    // starting a WebApplicationFactory host is expensive, and none of these tests need
    // isolation from each other (each provisions its own partner, so rate-limit state
    // never crosses between tests).
    public class OrdersContractTest: IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public OrdersContractTest(WebApplicationFactory<Program> factory)
        {
            // Seeting the partner rate limit down to something tiny just for this test
            // host, so the 429 test can exhaust it in 4 requests instead of the real
            // configured 100/60s
            Environment.SetEnvironmentVariable("RateLimiting__PartnerPolicy__PermitLimit", "3");
            Environment.SetEnvironmentVariable("RateLimiting__PartnerPolicy__WindowSeconds", "60");
            Environment.SetEnvironmentVariable("RateLimiting__PartnerPolicy__QueueLimit", "0");

            _factory = factory;
        }

        // Signs the given client in as the test admin, then provisions a brand-new
        // partner through the real admin API and returns its raw API key. Each call
        // creates a distinct partner, so tests never share rate-limit state.
        private static async Task<string> ProvisionPartnerApiKeyAsync(HttpClient adminClient)
        {
            var loginResponse = await adminClient.PostAsync("/api/test/admin-login", null);
            loginResponse.EnsureSuccessStatusCode();

            var provisionResponse = await adminClient.PostAsJsonAsync(
                "api/v1/partners",
                new ProvisionPartnerRequest($"contract-test-{Guid.NewGuid()}"));
            provisionResponse.EnsureSuccessStatusCode();

            var body = await provisionResponse.Content.ReadFromJsonAsync<ProvisionPartnerResponse>();

            return body!.ApiKey;
        }

        [Fact]
        public async Task PartnerOverRateLimit_Returns429_AdminRemainsUnaffected()
        {
            // Arrange
            // admin client provisions a partner key through the real flow
            var adminClient = _factory.CreateClient();
            var apiKey = await ProvisionPartnerApiKeyAsync(adminClient);

            var partnerClient = _factory.CreateClient();
            partnerClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            // Act
            // exhause the overidden PermitLimit = 3 for this partner
            for (int i = 0; i < 3; i++)
            {
                var response = await partnerClient.GetAsync("/api/v1/orders?customerId=1&status=Completed");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            var throttledResponse = await partnerClient.GetAsync("api/v1/orders?customerId=1&status=Completed");

            // Assert
            // partner gets throttled with a Retry-After header
            Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
            Assert.True(throttledResponse.Headers.Contains("Retry-After"));

            // Admin should be unaffected with partner throttling
            for(int i = 0; i<5; i++)
            {
                var adminResponse = await adminClient.GetAsync("/api/v1/orders?customerId=1&status=Completed");
                Assert.NotEqual(HttpStatusCode.TooManyRequests, adminResponse.StatusCode);
            }
        }

        [Fact]
        public async Task GetOrders_V1ResponseShape_Unchanged()
        {
            // Arrange
            var adminClient = _factory.CreateClient();
            var loginResponse = await adminClient.PostAsync("/api/test/admin-login", null);
            loginResponse.EnsureSuccessStatusCode();

            // Act
            var response = await adminClient.GetAsync("/api/v1/orders?customerId=1&status=Completed");
            response.EnsureSuccessStatusCode();

            // Assert
            // Deserializing into Orderv1Response alone would not catch accidentally added field
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.True(doc.RootElement.GetArrayLength() > 0, "Expected seeded orders for customerId=1/Completed");

            var expectedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "orderId", "customerId", "total","status"
            };

            var actualFields = doc.RootElement[0].EnumerateObject()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Equal(expectedFields, actualFields);

        }

        [Fact]
        public async Task GetOrders_MalformedStatus_ReturnsWithUsefulError()
        {
            // Arrange
            var adminClient = _factory.CreateClient();
            var loginResponse = await adminClient.PostAsync("/api/test/admin-login", null);
            loginResponse.EnsureSuccessStatusCode();

            // Act
            // status isn't define in OrderStatus value
            var response = await adminClient.GetAsync("/api/v1/orders?customerId=1&status=NotRealStatus");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
            Assert.False(string.IsNullOrWhiteSpace(errorProp.GetString()));

        }
    }
}
