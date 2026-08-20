using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using NexusUnleashed.Network;

namespace NexusUnleashed.Database;

public sealed class DbCharacterStore
{
    private readonly string _connectionString;

    public DbCharacterStore(string authConnectionString)
    {
        var b = new MySqlConnectionStringBuilder(authConnectionString) { Database = "characterdb" };
        _connectionString = b.ConnectionString;
    }

    public async Task<List<CharacterRecord>> GetCharactersAsync(long accountId)
    {
        var list = new List<CharacterRecord>();
        if (accountId <= 0) return list;

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            "SELECT id, name, sex, race, class, level, factionId, " +
            "locationX, locationY, locationZ, worldId " +
            "FROM `character` WHERE accountId = @acc AND deleteTime IS NULL " +
            "ORDER BY id", conn);
        cmd.Parameters.AddWithValue("@acc", accountId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CharacterRecord
            {
                Id        = (ulong)reader.GetInt64(0),
                Name      = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Sex       = reader.GetByte(2),
                Race      = reader.GetByte(3),
                Class     = reader.GetByte(4),
                Level     = reader.GetByte(5),
                FactionId = (uint)reader.GetInt16(6),
                LocationX = reader.GetFloat(7),
                LocationY = reader.GetFloat(8),
                LocationZ = reader.GetFloat(9),
                WorldId   = (uint)reader.GetInt16(10),
            });
        }
        return list;
    }
}
