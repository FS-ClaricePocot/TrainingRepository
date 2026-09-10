using Microsoft.Extensions.Caching.Memory;
using OrderManagement.Api.Dtos;
using Microsoft.Data.SqlClient;

namespace OrderManagement.Api.Reports
{
    public class ReportService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache _jobStore;
        private readonly ReportJobQueue _queue;

        // how long a completed/failed jobs record stary retrievable
        private static readonly TimeSpan _jobRetention = TimeSpan.FromMinutes(10);

        // Simulation only for production-scale report
        // Currently, real query with 50k rows only takes ~126ms
        public static readonly TimeSpan _simulatedProcessingDelay = TimeSpan.FromSeconds(5);

        public ReportService(string connectionString, IMemoryCache jobStore, ReportJobQueue queue)
        {
            _connectionString = connectionString;
            _jobStore = jobStore;
            _queue = queue;
        }

        public async Task<Guid> QueueReportAsync(string groupBy)
        {
            var jobId = Guid.NewGuid();
            _jobStore.Set(jobId, new ReportJobRecord { Status = ReportJobStatus.Pending }, _jobRetention);

            await _queue.EnqueueAsync(new ReportJobRequest(jobId, groupBy));
            return jobId;
        }

        public ReportJobRecord? GetJob(Guid jobId)
        {
            return _jobStore.TryGetValue(jobId, out ReportJobRecord? record) ? record : null;
        }

        public async Task ProcessReportAsync(Guid jobId, string groupBy, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_simulatedProcessingDelay, cancellationToken);

                var parsedGroupBy = Enum.Parse<ReportGroupBy>(groupBy);
                var result = parsedGroupBy == ReportGroupBy.ByStatus
                    ? await GenerateByStatusAsync(cancellationToken)
                    : await GenerateByCustomerAsync(cancellationToken);

                _jobStore.Set(jobId, new ReportJobRecord
                {
                    Status = ReportJobStatus.Completed,
                    Result = result
                }, _jobRetention);
            }
            catch (Exception)
            {
                _jobStore.Set(jobId, new ReportJobRecord
                {
                    Status = ReportJobStatus.Failed,
                    Error = "An internal error occured while generating the report."

                }, _jobRetention);
            }
        }

        private async Task<List<ReportGroupResult>> GenerateByCustomerAsync(CancellationToken cancellationToken)
        {
            var results = new List<ReportGroupResult>();
            string query = @"SELECT o.CustomerId, c.Name, COUNT(*) AS OrderCount, SUM(o.Total) AS TotalSum
                             FROM orders o
                             JOIN customers c ON o.CustomerId = c.CustomerId
                             GROUP BY o.CustomerId, c.Name";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ReportGroupResult
                {
                    CustomerId = reader.GetInt32(0),
                    CustomerName = reader.GetString(1),
                    OrderCount = reader.GetInt32(2),
                    TotalSum = reader.GetDecimal(3)

                });
            }

            return results;

        }

        private async Task<List<ReportGroupResult>> GenerateByStatusAsync(CancellationToken cancellationToken)
        {
            var results = new List<ReportGroupResult>();
            string query = @"SELECT o.Status, COUNT(*) AS OrderCount, SUM(o.Total) AS TotalSum 
                             FROM Orders o
                             JOIN Customers c ON o.CustomerId = c.CustomerId
                             GROUP BY o.Status";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while(await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ReportGroupResult
                {
                    Status = reader.GetString(0),
                    OrderCount = reader.GetInt32(1),
                    TotalSum = reader.GetDecimal(2)
                });
            }

            return results;
        }
    }


}
