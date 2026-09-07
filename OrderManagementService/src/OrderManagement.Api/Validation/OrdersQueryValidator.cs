using OrderManagement.Core.Enums;

namespace OrderManagement.Api.Validation
{
    // Shared validation for the customerId/status query params, used by
    // both OrdersV1Controller and OrdersV2Controller so the rule lives in
    // exactly one place.
    public static class OrdersQueryValidator
    {
        public static bool IsValid(int customerId, string status)
        {
            return customerId > 0 && IsValidStatus(status);
        }

        // Split out so PATCH .../status (which has no customerId to check)
        // can reuse the same status rule instead of duplicating it.
        public static bool IsValidStatus(string status)
        {
            return !string.IsNullOrEmpty(status) && Enum.IsDefined(typeof(OrderStatus), status);
        }
    }
}
