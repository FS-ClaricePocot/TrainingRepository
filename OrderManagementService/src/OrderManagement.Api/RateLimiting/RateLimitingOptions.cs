namespace OrderManagement.Api.RateLimiting
{
    // Bound from the "RateLimiting" config section so limits can be tuned
    // per environment without a code change (see design-doc-dual-consumer-orders.md).
    public class RateLimitPolicySettings
    {
        public int PermitLimit { get; set; }
        public int WindowSeconds { get; set; }
        public int QueueLimit { get; set; }
    }

    public class RateLimitingOptions
    {
        public const string SectionName = "RateLimiting";

        public RateLimitPolicySettings PartnerPolicy { get; set; } = new();
        public RateLimitPolicySettings InternalPolicy { get; set; } = new();
    }
}
