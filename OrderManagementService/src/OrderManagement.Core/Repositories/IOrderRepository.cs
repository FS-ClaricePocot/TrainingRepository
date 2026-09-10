namespace OrderManagement.Core.Repositories
{
    // Data-access seam for OrderService. Lets tests substiture a mock for the SQL calls
    // so caching / invalidation logic stays unit-testable
    public interface IOrderRepository
    {
        Task<List<Order>> LoadOrdersAsync(int customerId, string status);
        Task<(int? CustomerId, string? status)> GetOrderCustomerAndStatusAsync(int orderId);
        Task UpdateOrderStatusAsync(int orderId, string newStatus);

    }
}
