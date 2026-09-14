namespace OrderManagement.Api.RateLimiting
{
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

        // Documentary only - the internal admin has no limits (calls GetNoLimiter)
        // and does not read these values from settings
        // PermitLimit/WindowSeconds/QueueLimit = -1 signals "no limit" to a
        // human reader
        public RateLimitPolicySettings InternalPolicy { get; set; } = new();
    }
}
