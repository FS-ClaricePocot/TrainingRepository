using Microsoft.Data.SqlClient;

namespace OrderManagement.Tests.Infrastructure;

// Creates a uniquely named, throwaway database on the SQL Server named by the
// ORDERMANAGEMENT_TEST_SQL environment variable (server-level connection string, no
// Database=; defaults to the local server with Windows auth), builds the schema plus a
// small deterministic seed, and drops it again on teardown.
//
// Destructive tests (SQL-injection payloads) and tests that write (partner
// provisioning) run against this - never against the shared dev database.
public sealed class TestDatabase: IAsyncLifetime
{
    public const string ServerConnectionEnvVar = "ORDERMANAGEMENT_TEST_SQL";
    private const string DefaultServerConnection = "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True;";

    // What seed contains. Test Assert against these
    public const int SeededCustomerId = 1;
    public const int SeededCompletedOrderCount = 2;
    public const int SeededTotalOrderCount = 4;

    private const string SchemaAndSeedSql = """
            CREATE TABLE Customers (
                CustomerId INT PRIMARY KEY IDENTITY(1,1),
                Name NVARCHAR(200) NOT NULL,
                Email NVARCHAR(320) NOT NULL,
                IsActive BIT NOT NULL DEFAULT 1
            );
            CREATE UNIQUE INDEX UQ_Customers_Email ON Customers(Email);

            CREATE TABLE Orders (
                OrderId INT PRIMARY KEY IDENTITY(1,1),
                CustomerId INT NOT NULL REFERENCES Customers(CustomerId),
                Total DECIMAL(10,2) NOT NULL,
                Status NVARCHAR(50) NOT NULL
            );
            CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Status ON Orders (CustomerId, Status);

            CREATE TABLE Partners (
                PartnerId INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(200) NOT NULL
            );
            CREATE TABLE PartnerApiKeys (
                KeyId INT IDENTITY(1,1) PRIMARY KEY,
                PartnerId INT NOT NULL FOREIGN KEY REFERENCES Partners(PartnerId),
                HashedKey NVARCHAR(64) NOT NULL UNIQUE,
                Status NVARCHAR(20) NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                RotatingSince DATETIME2 NULL,
                RevokedAt DATETIME2 NULL
            );

            INSERT INTO Customers (Name, Email) VALUES
                ('Test Customer One', 'one@example.com'),
                ('Test Customer Two', 'two@example.com');
            INSERT INTO Orders (CustomerId, Total, Status) VALUES
                (1, 150.00, 'Completed'),
                (1,  75.50, 'Completed'),
                (1,  40.00, 'Pending'),
                (2,  20.00, 'Completed');
            """;

    private readonly string _serverConnectionString =
            Environment.GetEnvironmentVariable(ServerConnectionEnvVar) ?? DefaultServerConnection;

    private readonly string _databaseName = $"OrderManagementDb_Test_{Guid.NewGuid():N}";
    public string ConnectionString { get; private set; } = String.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            await ExecuteAsync(_serverConnectionString, $"CREATE DATABASE [{_databaseName}]");
        }
        catch(SqlException exception)
        {
            throw new InvalidOperationException(
                "Could not create a throwaway test database. These tests need a reachable SQL Server " +
                "where the current login can CREATE DATABASE. Set the " + ServerConnectionEnvVar +
                " environment variable to a server-level connection string (no Database=) to point at a " +
                "different server. Default: " + DefaultServerConnection, exception);

        }

        ConnectionString = new SqlConnectionStringBuilder(_serverConnectionString)
        {
            InitialCatalog = _databaseName
        }.ConnectionString;

        try
        {
            await ExecuteAsync(ConnectionString, SchemaAndSeedSql);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        // Pooled connections keep the database "in use" - clear them

        SqlConnection.ClearAllPools();
        await ExecuteAsync(_serverConnectionString, $@"
            IF DB_ID('{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END");
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }
    private async Task ExecuteAsync(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}