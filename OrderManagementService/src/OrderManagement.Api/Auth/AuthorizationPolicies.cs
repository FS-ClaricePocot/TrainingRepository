using Microsoft.AspNetCore.Authentication.Cookies;
using System.Runtime.CompilerServices;

namespace OrderManagement.Api.Auth
{
    public static class AuthorizationPolicyNames
    {
        public const string InternalAdmin = "InternalAdmin";
        public const string Partner = "Partner";
        public const string DualConsumer = "DualConsumer";
    }
    public static class AuthorizationPolicies
    {
        public static void AddOrderApiAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationPolicyNames.InternalAdmin, policy =>
                {
                    policy.AuthenticationSchemes.Add(
                        Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();

                });

                options.AddPolicy(AuthorizationPolicyNames.Partner, policy =>
                {
                    policy.AuthenticationSchemes.Add(ApiKeyAuthConstants.SchemeName);
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy(AuthorizationPolicyNames.DualConsumer, policy =>
                {
                    policy.AuthenticationSchemes.Add(CookieAuthenticationDefaults.AuthenticationScheme);
                    policy.AuthenticationSchemes.Add(ApiKeyAuthConstants.SchemeName);
                    policy.RequireAuthenticatedUser();
                });
            });

            
        }
    }
}
