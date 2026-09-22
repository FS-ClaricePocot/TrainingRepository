using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using OrderManagement.Core.Repositories;
using System.Collections.Concurrent;

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

    // Coordinates concurrent cache-miss requests for the same key so the DB
    // is only hit once per miss, no matter how many callers race in at the
    // same time. Self-cleaning - an entry only lives here for the duration
    // of one in-flight load, then it's removed, so this can't grow
    // unbounded the way the old _keyLocks dictionary did.
    private readonly ConcurrentDictionary<string, Lazy<Task<List<Order>>>> _inFlightLoads = new();

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

        // If already cached, return right away
        if (_cache.TryGetValue(cacheKey, out List<Order>? cached))
        {
            return cached!;
        }

        // If miss - coordinate with any other concurrent callers racing on the
        // exact same key. GetOrAdd guarantees every racer receives the same
        // Lazy instance, so the DB call only actually happens once no
        // matter how many threads land here simultaneously.
        var lazyLoad = _inFlightLoads.GetOrAdd(
            cacheKey,
            key => new Lazy<Task<List<Order>>>(() => LoadAndCacheAsync(key, customerId, status)));
       

        try
        {
            //Triggers the actual call to the DB
            return await lazyLoad!.Value;
        }
        finally
        {
            // Remove once resolved (success or failure) - it was only ever
            // needed to coordinate the in-flight race, not as a cache.
            _inFlightLoads.TryRemove(cacheKey, out _);
        }
    }

    private async Task<List<Order>> LoadAndCacheAsync(string cacheKey, int customerId, string status)
    {
        var orders = await _repository.LoadOrdersAsync(customerId, status);
        _cache.Set(cacheKey, orders, _cacheTtl);
        return orders;
    }

    public async Task<decimal> GetTotalSpendAsync(int customerId)
    {
        var orders = await GetOrdersForCustomerAsync(customerId, "Completed");
        var total = orders.Where(o => o.Status == "Completed").Sum(o => o.Total);
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
