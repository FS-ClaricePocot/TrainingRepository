using Microsoft.Data.SqlClient;

namespace OrderManagement.Api.Partners
{
    public class SqlPartnerRepository : IPartnerRepository
    {
        private readonly string _connectionString;

        public SqlPartnerRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<Partner> CreatePartnerAsync(string name)
        {
            const string query = "INSERT INTO Partners (NAME) OUTPUT INSERTED.PartnerId VALUES (@name)";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", name);

            var partnerId = (int)(await cmd.ExecuteScalarAsync())!;
            return new Partner { PartnerId = partnerId, Name = name };
        }
        public async Task<PartnerApiKey> CreateKeyAsync(int partnerId, string hashedKey, PartnerKeyStatus status)
        {
            const string query = @"
                INSERT INTO PartnerApiKeys (PartnerId, HashedKey, Status, CreatedAt)
                OUTPUT INSERTED.KeyId, INSERTED.CreatedAt
                VALUES (@partnerId, @hashedKey, @status, SYSUTCDATETIME())";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@partnerId", partnerId);
            cmd.Parameters.AddWithValue("@hashedKey", hashedKey);
            cmd.Parameters.AddWithValue("@status", status.ToString());

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return new PartnerApiKey
            {
                KeyId = reader.GetInt32(0),
                CreatedAt = reader.GetDateTime(1),
                PartnerId = partnerId,
                HashedKey = hashedKey,
                Status = status
            };
        }

        public async Task<PartnerApiKey?> FindByHashedKeyAsync(string hashedKey)
        {
            const string query = @"
               SELECT KeyId, PartnerId, HashedKey, Status, CreatedAt, RotatingSince, Revokedat
               FROM PartnerApiKeys
               WHERE HashedKey = @hashedKey";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@hashedKey", hashedKey);

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapKey(reader) : null;

        }

        public async Task<PartnerApiKey?> GetActiveKeyForPartnerAsync(int partnerId)
        {
            const string query = @"
                SELECT KeyId, PartnerId, HashedKey, Status, CreatedAt, RotatingSince, RevokedAt
                FROM PartnerApiKeys
                WHERE PartnerId = @partnerId AND Status = 'Active'";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@partnerId", partnerId);

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapKey(reader) : null;
        }

        public async Task<Partner?> GetPartnerAsync(int partnerId)
        {
            const string query = "SELECT PartnerId, Name FROM Partners WHERE PartnerId = @partnerId";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@partnerId", partnerId);

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() 
                ? new Partner { PartnerId = reader.GetInt32(0), Name = reader.GetString(1) }
                : null;
        }

        public async Task<bool> MarkRevokedAsync(int partnerId, int keyId)
        {
            const string query = @"
                UPDATE PartnerApiKeys
                SET Status = 'Revoked', RevokedAt = COALESCE(Revokedat,SYSUTCDATETIME())
                WHERE KeyId = @keyId AND PartnerId = @partnerId";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@partnerId", partnerId);
            cmd.Parameters.AddWithValue("@keyId", keyId);
            
            // no rows returned means no matched keys found to revoke
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task MarkRotatingAsync(int keyId)
        {
            const string query = @"
                UPDATE PartnerApiKeys
                SET Status = 'Rotating', RotatingSince = SYSUTCDATETIME()
                WHERE KeyId = @keyId";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@keyId", keyId);
            await cmd.ExecuteNonQueryAsync();
        }

        private static PartnerApiKey MapKey(SqlDataReader reader) => new()
        {
            KeyId = reader.GetInt32(0),
            PartnerId = reader.GetInt32(1),
            HashedKey = reader.GetString(2),
            Status = Enum.Parse<PartnerKeyStatus>(reader.GetString(3)),
            CreatedAt = reader.GetDateTime(4),
            RotatingSince = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            RevokedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
        };

    }
}
