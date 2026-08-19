// NexusUnleashed - clean-room authored. Minimal server configuration loaded from
// a JSON file beside the executable. Our own config surface.
using System.IO;
using System.Text.Json;

namespace NexusUnleashed.Realm;

public sealed class RealmConfig
{
    public string BindAddress { get; set; } = "0.0.0.0";
    public int AuthPort { get; set; } = 23115;   // UNPINNED: real ports from oracle
    public int WorldPort { get; set; } = 24000;
    public string Database { get; set; } = "Server=127.0.0.1;Port=3307;User=root;Database=worlddb";
    public string AssetPath { get; set; } = "./assets";

    public static RealmConfig Load(string path)
    {
        if (!File.Exists(path))
            return new RealmConfig();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<RealmConfig>(File.ReadAllText(path), opts)
               ?? new RealmConfig();
    }
}
