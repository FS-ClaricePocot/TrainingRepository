using Microsoft.Data.SqlClient;


namespace OrderManagement.Core.Repositories
{
    public class SqlOrderRepository : IOrderRepository
    {
        private readonly string _connectionString;
        public SqlOrderRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Order>> LoadOrdersAsync(int customerId, string status)
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
                    orders.Add(new Order
                    {
                        OrderId = reader.GetInt32(0),
                        CustomerId = reader.GetInt32(1),
                        Total = reader.GetDecimal(2),
                        Status = reader.GetString(3)
                    });
                }
            }

            return orders;
        }


        public async Task<(int? CustomerId, string? status)> GetOrderCustomerAndStatusAsync(int orderId)
        {
            int? customerId = null;
            string? status = null;
            string query = "SELECT CustomerId, Status FROM Orders WHERE OrderId = @orderId";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@orderId", orderId);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    customerId = reader.GetInt32(0);
                    status = reader.GetString(1);
                }
            }

            return (customerId, status);
        }

        public async Task UpdateOrderStatusAsync(int orderId, string newStatus)
        {
            string query = "UPDATE Orders SET Status = @status WHERE OrderId = @orderId";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@status", newStatus);
                cmd.Parameters.AddWithValue("@orderId", orderId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

    }
}
