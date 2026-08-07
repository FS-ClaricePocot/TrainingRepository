using System.Data;
using Microsoft.Data.SqlClient;

public class Order
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
}

// Seam that lets tests swap the real SQL connection for a mock (e.g. via Moq)
// without OrderService knowing or caring which one it got.
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection(string connectionString);
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    public IDbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);
}

public class OrderService
{
    private static Dictionary<(int, string), List<Order>> _cache;
    private readonly string _connectionString;
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderService(string connectionString, Dictionary<(int, string), List<Order>> cache)
        : this(connectionString, cache, new SqlConnectionFactory())
    {
    }

    public OrderService(string connectionString, Dictionary<(int, string), List<Order>> cache, IDbConnectionFactory connectionFactory)
    {
        _connectionString = connectionString;
        _cache = cache;
        _connectionFactory = connectionFactory;
    }

    public List<Order> GetOrderForCustomer(int customerId, string status)
    {
        if (string.IsNullOrEmpty(status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
        }

        if (_cache.ContainsKey((customerId, status)))
        {
            return _cache[(customerId, status)];
        }

        var orders = new List<Order>();
        string query = "SELECT OrderId, CustomerId, Total, Status FROM Orders WHERE CustomerId = @customerId AND Status = @status";

        using (var conn = _connectionFactory.CreateConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            AddParameter(cmd, "@customerId", customerId);
            AddParameter(cmd, "@status", status);
            using var reader = cmd.ExecuteReader();

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
        if (orderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderId), "Order ID must be a positive integer");
        }

        if (string.IsNullOrEmpty(newStatus))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(newStatus));
        }

        string query = "UPDATE Orders SET Status = @newStatus WHERE OrderId = @orderId";
        using (var conn = _connectionFactory.CreateConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            AddParameter(cmd, "@newStatus", newStatus);
            AddParameter(cmd, "@orderId", orderId);
            cmd.ExecuteNonQuery();
        }
        _cache.Clear();
    }

    private static void AddParameter(IDbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(param);
    }
}
