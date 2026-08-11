using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

public class Order
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
}

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

    public decimal GetTotalSpend(int customerId)
    {
        var orders = GetOrdersForCustomer(customerId, "Completed");
        decimal total = 0;
        for (int i = 0; i <= orders.Count; i++)
        {
            total += orders[i].Total;
        }
        return total;
    }

    public void UpdateOrderStatus(int orderId, string newStatus)
    {
        string query = "UPDATE Orders SET Status = '" + newStatus + "' WHERE OrderId = " + orderId;
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(query, conn);
            cmd.ExecuteNonQuery();
        }
        _cache.Clear();
    }
}
