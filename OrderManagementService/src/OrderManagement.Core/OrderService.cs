using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using OrderManagement.Core.Repositories;

public class Order
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
}

public class OrderService
{
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    private readonly IOrderRepository _repository;

    public OrderService(IOrderRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    private static string BuildCacheKey(int customerId, string status)
    {
        return $"{customerId}:{status}";
    }

    public async Task<List<Order>> GetOrdersForCustomerAsync(int customerId, string status)
    {
        string cacheKey = BuildCacheKey(customerId, status);

        //Returns existing unexpired cached value or creates new entry with the call to DB
        var lazyOrders = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheTtl;
            return new Lazy<Task<List<Order>>>(() => _repository.LoadOrdersAsync(customerId, status));
        });

        try
        {
            //Triggers the actual call to the DB
            return await lazyOrders!.Value;
        }
        catch
        {
            //Removes bad entry in the event of DB call failure
            _cache.Remove(cacheKey);
            throw;
        }
    }


    public async Task<decimal> GetTotalSpendAsync(int customerId)
    {
        var orders = await GetOrdersForCustomerAsync(customerId, "Completed");
        var total = orders.Sum(o => o.Total);
        return total;
    }

    public async Task UpdateOrderStatusAsync(int orderId, string newStatus)
    {
        //Look up current details from DB for this order before updating
        var (customerId, oldStatus) = await _repository.GetOrderCustomerAndStatusAsync(orderId);

        if (customerId is null)
        {
            //Order does not exist, nothing to update in DB or to invalidate in cache
            throw new InvalidOperationException($"Order {orderId} was not found.");
        }

        await _repository.UpdateOrderStatusAsync(orderId, newStatus);

        //Invalidate existing stale value in cache
        _cache.Remove(BuildCacheKey(customerId.Value, oldStatus!));
        _cache.Remove(BuildCacheKey(customerId.Value, newStatus));
    }

}
