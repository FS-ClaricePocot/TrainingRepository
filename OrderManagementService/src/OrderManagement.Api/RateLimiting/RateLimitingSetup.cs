using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using OrderManagement.Api.Auth;

namespace OrderManagement.Api.RateLimiting
{
    public static class RateLimitPolicyNames
    {
        // One policy attached to every dual-consumer endpoint. It branches
        // internally by resolved identity rather than having two separate
        // named policies, because the same action (e.g. GET orders) serves
        // both partner and admin traffic - there's no separate endpoint to
        // hang a second policy off of.
        public const string OrdersApi = "OrdersApi";
    }

    public static class RateLimitingSetup
    {
        public static void AddOrderApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                ?? throw new InvalidOperationException($"Missing '{RateLimitingOptions.SectionName}' configuration section.");

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                    }

                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new { error = "Rate limit exceeded. Please retry later." },
                        cancellationToken);
                };

                options.AddPolicy(RateLimitPolicyNames.OrdersApi, httpContext =>
                {
                    var identity = httpContext.User.Identity;

                    var isAdmin = identity is { IsAuthenticated: true }
                        && identity.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme;
                    if (isAdmin)
                    {
                        var adminId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? identity!.Name
                            ?? "unknown-admin";
                        return RateLimitPartition.GetFixedWindowLimiter(
                            $"admin:{adminId}",
                            _ => ToFixedWindowOptions(settings.InternalPolicy));
                    }

                    var isPartner = identity is { IsAuthenticated: true }
                        && identity.AuthenticationType == ApiKeyAuthConstants.SchemeName;
                    if (isPartner)
                    {
                        var partnerId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? "unknown-partner";
                        return RateLimitPartition.GetFixedWindowLimiter(
                            $"partner:{partnerId}",
                            _ => ToFixedWindowOptions(settings.PartnerPolicy));
                    }

                    // [Authorize] enforcement isn't wired onto the controllers yet (separate,
                    // still-pending task), so requests can still reach here unauthenticated.
                    // Fall back to the stricter partner-level limits, partitioned by IP,
                    // instead of defaulting to the generous internal ceiling - an unresolved
                    // identity should never get the loose admin allowance.
                    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"anonymous:{remoteIp}",
                        _ => ToFixedWindowOptions(settings.PartnerPolicy));
                });
            });
        }

        private static FixedWindowRateLimiterOptions ToFixedWindowOptions(RateLimitPolicySettings settings) => new()
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueLimit = settings.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };
    }
}
