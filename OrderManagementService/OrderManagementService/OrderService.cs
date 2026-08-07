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
    private static Dictionary<(int, string), List<Order>> _cache;
    private readonly string _connectionString;

    public OrderService(string connectionString, Dictionary<(int, string), List<Order>> cache)
    {
        _connectionString = connectionString;
        _cache = cache;
    }

    public List<Order> GetOrderForCustomer(int customerId, string status)
    {
        if (_cache.ContainsKey((customerId, status)))
        {
            return _cache[(customerId, status)];
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

        _cache[(customerId, status)] = orders;
        return orders;
    }

    public decimal GetTotalSpend(int customerId)
    {
        var orders = GetOrderForCustomer(customerId, "Completed");
        return orders.Sum(o => o.Total);        
    }

    public void UpdateOrderStatus(int orderId, string newStatus)
    {
        string query = "UPDATE Orders SET Status = @newStatus WHERE OrderId = @orderId";
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@newStatus", newStatus);
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.ExecuteNonQuery();
        }
        _cache.Clear();
    }
}