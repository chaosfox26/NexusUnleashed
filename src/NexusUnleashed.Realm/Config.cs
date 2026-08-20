using System.IO;
using System.Text.Json;

namespace NexusUnleashed.Realm;

public sealed class RealmConfig
{
    public string RealmName { get; set; } = "NexusUnleashed";
    public string MessageOfTheDay { get; set; } =
        "Welcome to NexusUnleashed - a clean-room WildStar realm. Open to all, owned by none.";

    public string BindAddress { get; set; } = "0.0.0.0";
    public int StsPort { get; set; } = 6600;    public int AuthPort { get; set; } = 23115;    public int WorldPort { get; set; } = 24000;
    public string Database { get; set; } = "Server=127.0.0.1;Port=3307;User=root;Database=worlddb";
    public string AuthDatabase { get; set; } = "";
    public string ContentRoot { get; set; } = "./content";
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
