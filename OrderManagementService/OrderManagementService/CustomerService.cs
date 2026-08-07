using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;

namespace OrderManagement.Core
{
    public class  Customer
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }   
        public bool IsActive { get; set; }
    }
    public class CustomerService
    {
        private static Dictionary<string, Customer> _cache;
        private readonly string _connectionString;

        public CustomerService(string connectionString, Dictionary<string, Customer> cache )
        {
            _connectionString = connectionString;
            _cache = cache;
        }

        public Customer GetCustomerByEmail(string email)
        {
            if(string.IsNullOrEmpty(email))
            {
                throw new ArgumentException("Email cannot be null or empty", nameof(email));
            }

            if (_cache.ContainsKey(email))
            {
                return _cache[email];
            }

            Customer customer = null;
            string query = "SELECT CustomerId, Name, Email, IsActive FROM Customers WHERE Email = @email";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@email", email);
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    customer = new Customer();
                    customer.CustomerId = reader.GetInt32(0);
                    customer.Name = reader.GetString(1);
                    customer.Email = reader.GetString(2);
                    customer.IsActive = reader.GetBoolean(3);
                }
            }

            _cache[email] = customer;
            return customer;
        }

        public Customer GetCustomerById(int customerId)
        {
            foreach (var customer in _cache.Values)
            {
                if (customer.CustomerId == customerId)
                {
                    return customer;
                }
            }

            Customer customerFromDb = null;
            string query = "SELECT CustomerId, Name, Email, IsActive FROM Customers WHERE CustomerId = @customerId";
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@customerId", customerId);
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    customerFromDb = new Customer();
                    customerFromDb.CustomerId = reader.GetInt32(0);
                    customerFromDb.Name = reader.GetString(1);
                    customerFromDb.Email = reader.GetString(2);
                    customerFromDb.IsActive = reader.GetBoolean(3);
                }
            }
            if (customerFromDb != null)
            {
                _cache[customerFromDb.Email] = customerFromDb;
            }
            return customerFromDb;
        }

        public int CountActiveCustomers(int[] customerIds)
        {
            int active = 0;
            for (int i = 0; i < customerIds.Length; i++)
            {
                var c = GetCustomerById(customerIds[i]);
                if (c.IsActive)
                    active++;
            }
            return active;
        }

        public void DeactivateCustomer(int customerId)
        {
            string query = "UPDATE Customers SET IsActive = 0 WHERE CustomerId = @customerId";
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@customerId", customerId);
                cmd.ExecuteNonQuery();
            }
            _cache.Clear();
        }
    }
}
