# Day 4: What Breaks — Documentation

## The Problem

```csharp
public class OrderService
{
    private static Dictionary<int, List<Order>> _cache = new Dictionary<int, List<Order>>();
    private readonly string _connectionString;

    public OrderService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<Order> GetOrdersForCustomer(int customerId, string status)
    {
        if (_cache.ContainsKey(customerId))
        {
            return _cache[customerId];
        }

        var orders = new List<Order>();
        string query = "SELECT OrderId, CustomerId, Total, Status FROM Orders WHERE CustomerId = " + customerId +
                        " AND Status = '" + status + "'";

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(query, conn);
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

        _cache[customerId] = orders;
        return orders;
    }
}
```

Cache key is only using `customerId` instead of `customerId` and `status` as key. If there is an
order that matches the `customerId` stored in the cache, it will return this cached data even if
it does not match the status requested.

## Issue Reproduced

**Scenario:**

Orders table of `OrderManagementDb` has 3 orders for the same customer. There are 2 completed
orders and 1 Pending order.

In the scenario below, we first retrieved the Completed Orders for `CustomerId` 1 and it
correctly retrieved the 2 orders and stored it in the cache. During the second request however,
we retrieved the pending orders for the same customer. Since the `CustomerId` matches, it
returned the exact same data stored in the cache from the first call, completely ignoring the
order status.

```sql
select orders.CustomerId, Email, OrderId, Total, [Status] from dbo.Orders orders
join dbo.Customers customers on orders.CustomerId = customers.CustomerId
```

| CustomerId | Email             | OrderId | Total  | Status    |
|------------|-------------------|---------|--------|-----------|
| 1          | repro@example.com | 1       | 150.00 | Completed |
| 1          | repro@example.com | 2       | 75.50  | Completed |
| 1          | repro@example.com | 3       | 40.00  | Pending   |

**Repro output (before fix):**

```
COMPLETED ORDERS: 2 order(s)
  OrderId=1, Total=150.00, Status=Completed
  OrderId=2, Total=75.50, Status=Completed
PENDING ORDERS: 2 order(s)
  OrderId=1, Total=150.00, Status=Completed
  OrderId=2, Total=75.50, Status=Completed
```

The "Pending Orders" call returned the cached Completed-orders result instead of the actual
Pending order — the `status` argument was silently ignored on the second call because only
`customerId` was used as the cache key.

## Question: What would happen in production with 100 concurrent users hitting the same customer?

If 100 concurrent users hit the same customer, it will cause a cache stampede. All 100 requests
will hit SQL Server simultaneously with an identical query for one customer. That's a lot of
redundant operations.

## The Fixes Applied

### Composite key

**What changed:**

```csharp
private static string BuildCacheKey(int customerId, string status)
{
    return $"{customerId}:{status}";
}
```

Cache key now uses both `CustomerId` and `Status` to fix the issue with order status being
ignored in retrieval of order details.

### TTL

**What changed:**

```csharp
private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);
```

```csharp
_cache[cacheKey] = new CacheEntry
{
    Orders = orders,
    ExpiresAtUtc = DateTime.UtcNow.Add(_cacheTtl)
};
```

Cache key now has a time to live to avoid staleness. Keys have a safety net of being invalidated
even if the application missed invalidating them.

### Stampede guard

**What changed:**

```csharp
// Stampede guard: only one per caller per cache key hits the DB;
var gate = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
await gate.WaitAsync();

try
{
    if (_cache.TryGetValue(cacheKey, out var cachedOrder) && cachedOrder.ExpiresAtUtc > DateTime.UtcNow)
    {
        return cachedOrder.Orders;
    }
    // ...
}
```

This now ensures that only one caller per cache key hits the DB, fixing the previous issue
wherein the DB is bombarded with multiple redundant calls.

### Over-invalidation fix

**What changed:** `_cache.Clear()` → targeted `TryRemove` calls

```csharp
public void UpdateOrderStatus(int orderId, string newStatus)
{
    // Look up current details for this order before updating
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

    // Invalidate existing stale value in cache
    _cache.TryRemove(BuildCacheKey(customerId, oldStatus), out _);
    _cache.TryRemove(BuildCacheKey(customerId, newStatus), out _);
}
```

The previous approach removed all cache keys on update, causing deletion of cache keys unrelated
to the order status being updated.

## Proof of Fix

Ran the same calls with the fix and correct orders are now retrieved.

```
COMPLETED ORDERS: 2 order(s)
  OrderId=1, Total=150.00, Status=Completed
  OrderId=2, Total=75.50, Status=Completed
PENDING ORDERS: 1 order(s)
  OrderId=3, Total=40.00, Status=Pending
```