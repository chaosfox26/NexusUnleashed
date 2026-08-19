// NexusUnleashed - clean-room authored. Database-backed account store over the
// authdb `account` table. Schema (our own DB): id, email, s (SRP salt, hex),
// v (SRP verifier, hex), gameToken, sessionKey, createTime. SRP6a itself comes
// from NexusUnleashed.Cryptography. MySqlConnector is MIT-licensed.
using System;
using System.Globalization;
using System.Threading.Tasks;
using MySqlConnector;
using NexusUnleashed.Sts;

namespace NexusUnleashed.Database;

public sealed class DbAccountStore : IAccountStore
{
    private readonly string _connectionString;

    public DbAccountStore(string connectionString)
        => _connectionString = connectionString;

    public async Task<(byte[] Salt, byte[] Verifier)?> GetSrpCredentialsAsync(string loginName)
    {
        if (string.IsNullOrWhiteSpace(loginName)) return null;

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            "SELECT s, v FROM account WHERE email = @email LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@email", loginName);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        string s = reader.GetString(0);
        string v = reader.GetString(1);
        return (FromHex(s), FromHex(v));
    }

    public async Task StoreGameTokenAsync(string loginName, Guid token)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE account SET gameToken = @tok WHERE email = @email", conn);
        // gameToken is varchar(32): 16 bytes as 32 hex chars, matching the salt width.
        cmd.Parameters.AddWithValue("@tok", token.ToString("N"));
        cmd.Parameters.AddWithValue("@email", loginName);
        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] FromHex(string hex)
    {
        int n = hex.Length / 2;
        var b = new byte[n];
        for (int i = 0; i < n; i++)
            b[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return b;
    }
}
