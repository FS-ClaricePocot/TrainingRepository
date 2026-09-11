using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

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

    private readonly string _connectionString;

    public OrderService(string connectionString, IMemoryCache cache)
    {
        _connectionString = connectionString;
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
            return new Lazy<Task<List<Order>>>(() => LoadOrdersFromDbAsync(customerId, status));
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

    public async Task<List<Order>> LoadOrdersFromDbAsync(int customerId, string status)
    {
        var orders = new List<Order>();
        string query = "SELECT OrderId, CustomerId, Total, Status FROM Orders WHERE CustomerId = @customerId AND Status = @status";

        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@customerId", customerId);
            cmd.Parameters.AddWithValue("@status", status);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var order = new Order();
                order.OrderId = reader.GetInt32(0);
                order.CustomerId = reader.GetInt32(1);
                order.Total = reader.GetDecimal(2);
                order.Status = reader.GetString(3);
                orders.Add(order);
            }
        }

        return orders;
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
        var (customerId, oldStatus) = await GetOrderCustomerAndStatusAsync(orderId);

        if (customerId is null)
        {
            //Order does not exist, nothing to update in DB or to invalidate in cache
            throw new InvalidOperationException($"Order {orderId} was not found.");
        }


        string query = "UPDATE Orders SET Status = @newStatus WHERE OrderId = @orderId";
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@newStatus", newStatus);
            cmd.Parameters.AddWithValue("@orderId", orderId);
            await cmd.ExecuteNonQueryAsync();
        }

        //Invalidate existing stale value in cache
        _cache.Remove(BuildCacheKey(customerId.Value, oldStatus!));
        _cache.Remove(BuildCacheKey(customerId.Value, newStatus));
    }

    private async Task<(int? CustomerId, string? Status)> GetOrderCustomerAndStatusAsync(int orderId)
    {
        int? customerId = null;
        string? status= null;
        string queryCurrentStatus = "SELECT CustomerId, Status FROM Orders where OrderId = @orderId";
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand(queryCurrentStatus, conn);
            cmd.Parameters.AddWithValue("@orderId", orderId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                customerId = reader.GetInt32(0);
                status = reader.GetString(1);
            }
        }
        return (customerId,status);
    }
}
