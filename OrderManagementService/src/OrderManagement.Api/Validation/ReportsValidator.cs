using OrderManagement.Api.Reports;

namespace OrderManagement.Api.Validation
{
    public static class ReportsValidator
    {
        public static bool IsValidGroupBy(string groupBy)
        {
            return !string.IsNullOrEmpty(groupBy) && Enum.IsDefined(typeof(ReportGroupBy), groupBy);
        }
    }
}
