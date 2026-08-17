// connection string for local OrderManagementDb
const string connectionString = "Server=localhost;Database=OrderManagementDb;Trusted_Connection=True;TrustServerCertificate=True";

var reproCustomerId = 1; // customerID seeded in the db
var orderService = new OrderService(connectionString);

// retrieve completed orders for CustomerID = 1
// should return 2 completed orders
List<Order> completedOrders = await orderService.GetOrdersForCustomerAsync(reproCustomerId, "Completed");
Console.WriteLine($"COMPLETED ORDERS: {completedOrders.Count} order(s)");
foreach(var order in completedOrders)
{
    Console.WriteLine($"  OrderId={order.OrderId}, Total={order.Total}, Status={order.Status}");
}

//retrieve pending orders for CustomerID = 1
// should return 1 pending orders
List<Order> pendingOrders = await orderService.GetOrdersForCustomerAsync(reproCustomerId, "Pending");
Console.WriteLine($"PENDING ORDERS: {pendingOrders.Count} order(s)");
foreach (var order in pendingOrders)
{
    Console.WriteLine($"  OrderId={order.OrderId}, Total={order.Total}, Status={order.Status}");
}



