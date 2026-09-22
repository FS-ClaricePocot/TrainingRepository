using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderManagement.Api.Auth;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Tests.Infrastructure
{
    // One real, in-memory API host per test class, wired to its own throwaway database.
    //
    // - The connection string and the partner rate limit are applied with UseSetting. That is
    //   visible to Program.cs's eager config reads (ConfigureAppConfiguration is merged in too
    //   late for those) and is scoped to this host only - no process-wide environment variables.
    // - Admin auth is a real cookie minted with the host's own cookie options, so the tests don't
    //   depend on the #if DEBUG TestAuthController and pass in any build configuration.
    public sealed class ContractTestFixture : IAsyncLifetime
    {
        // Small on purpose, so rate-limit tests don't need to fire 100+ requests
        public const int PartnerPermitLimit = 3;

        private readonly TestDatabase _database = new();
        private TestHostFactory? _factory;

        public async Task InitializeAsync()
        {
            await _database.InitializeAsync();
            _factory = new TestHostFactory(_database.ConnectionString);
        }

        public async Task DisposeAsync()
        {
            _factory?.Dispose();
            await _database.DisposeAsync();
        }

        // A client authenticated as an internal admin (cookie scheme)
        public HttpClient CreateAdminClient()
        {
            var cookieOptions = _factory!.Services
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(CookieAuthenticationDefaults.AuthenticationScheme);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "test-admin") },
                CookieAuthenticationDefaults.AuthenticationScheme);

            var now = DateTimeOffset.UtcNow;
            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = now.AddHours(1) },
                CookieAuthenticationDefaults.AuthenticationScheme);

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("Cookie", $"{cookieOptions.Cookie.Name}={cookieOptions.TicketDataFormat.Protect(ticket)}");
            return client;
        }

        // Provisions a brand-new partner through the real admin API and returns a client that
        // authenticates with that partner's API key. Each call creates a distinct partner, so
        // tests never share rate-limit state, and it all lands in the throwaway database.
        public async Task<HttpClient> CreatePartnerClientAsync()
        {
            var admin = CreateAdminClient();
            var response = await admin.PostAsJsonAsync(
                "api/v1/partners",
                new ProvisionPartnerRequest($"contract-test-{Guid.NewGuid()}"));
            response.EnsureSuccessStatusCode();

            var partner = await response.Content.ReadFromJsonAsync<ProvisionPartnerResponse>();

            var client = _factory!.CreateClient();
            client.DefaultRequestHeaders.Add(ApiKeyAuthConstants.HeaderName, partner!.ApiKey);
            return client;
        }

        private sealed class TestHostFactory : WebApplicationFactory<Program>
        {
            private readonly string _connectionString;

            public TestHostFactory(string connectionString)
            {
                _connectionString = connectionString;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseSetting("ConnectionStrings:OrderManagementDb", _connectionString);
                builder.UseSetting("RateLimiting:PartnerPolicy:PermitLimit", PartnerPermitLimit.ToString());
                builder.UseSetting("RateLimiting:PartnerPolicy:WindowSeconds", "60");
                builder.UseSetting("RateLimiting:PartnerPolicy:QueueLimit", "0");
            }
        }
    }
}