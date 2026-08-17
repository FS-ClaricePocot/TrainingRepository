using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

public class Order
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
}

internal class CacheEntry
{
    public List<Order> Orders { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public class OrderService
{
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    private readonly string _connectionString;

    public OrderService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private static string BuildCacheKey(int customerId, string status)
    {
        return $"{customerId}:{status}";
    }

    public async Task<List<Order>> GetOrdersForCustomerAsync(int customerId, string status)
    {
        string cacheKey = BuildCacheKey(customerId, status);

        //Valid unexpired entry already present
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
        {
            return cached.Orders;
        }


        // Stampede guard: only one per caller per cache key hits the DB;
        var gate = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        try
        {
            if (_cache.TryGetValue(cacheKey, out var cachedOrder) && cachedOrder.ExpiresAtUtc > DateTime.UtcNow)
            {
                return cachedOrder.Orders;
            }

            var orders = new List<Order>();
            string query = "SELECT OrderId, CustomerId, Total, Status FROM Orders WHERE CustomerId = @customerId AND Status = @status";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@customerId", customerId);
                cmd.Parameters.AddWithValue("@status", status);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var order = new Order();
                    order.OrderId = reader.GetInt32(0);
                    order.CustomerId = reader.GetInt32(1);
                    order.Total = reader.GetDecimal(2);
                    order.Status = reader.GetString(3);
                    orders.Add(order);
                }
            }

            _cache[cacheKey] = new CacheEntry
            {
                Orders = orders,
                ExpiresAtUtc = DateTime.UtcNow.Add(_cacheTtl)
            };

            return orders;

        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<decimal> GetTotalSpendAsync(int customerId)
    {
        var orders = await GetOrdersForCustomerAsync(customerId, "Completed");
        var total = orders.Sum(o => o.Total);
        return total;
    }

    public void UpdateOrderStatus(int orderId, string newStatus)
    {
        //Look up current details for this order before updating
        var (customerId, oldStatus) = GetOrderCustomerAndStatus(orderId);


        string query = "UPDATE Orders SET Status = @newStatus WHERE OrderId = @orderId";
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@newStatus", newStatus);
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.ExecuteNonQuery();
        }

        //Invalidate existing stale value in cache
        _cache.TryRemove(BuildCacheKey(customerId, oldStatus), out _);
        _cache.TryRemove(BuildCacheKey(customerId, newStatus), out _);
    }

    private (int CustomerId, string Status) GetOrderCustomerAndStatus(int orderId)
    {
        int customerId = 0;
        string status= "";
        string queryCurrentStatus = "SELECT CustomerId, Status FROM Orders where OrderId = @orderId";
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(queryCurrentStatus, conn);
            cmd.Parameters.AddWithValue("@orderId", orderId);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                customerId = reader.GetInt32(0);
                status = reader.GetString(1);

            }
        }
        return (customerId,status);
    }
}
