using System.Diagnostics.Metrics;

namespace OrderManagement.Api.RateLimiting
{
    // Admin traffic is exempt from rate limiting by design, 
    // so this counter exists purely for observability
    public static class RateLimitingMetrics
    {
        private static readonly Meter Meter = new Meter("OrderManagement.Api.RateLimiting");
        public static readonly Counter<long> AdminRequests = Meter.CreateCounter<long>(
            "orders_api.admin_requests",
            description: "Count of requests identified as internal admin traffic (exempt from rate limiting).");

    }
}
