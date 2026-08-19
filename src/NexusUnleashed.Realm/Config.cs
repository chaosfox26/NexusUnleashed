// NexusUnleashed - clean-room authored. Minimal server configuration loaded from
// a JSON file beside the executable. Our own config surface.
using System.IO;
using System.Text.Json;

namespace NexusUnleashed.Realm;

public sealed class RealmConfig
{
    // Realm identity - OURS, never inherited. The connection banner and the
    // Message of the Day the client shows on login are defined here; the wire
    // message that carries the MotD is pinned from a capture, but its content
    // is always NexusUnleashed's.
    public string RealmName { get; set; } = "NexusUnleashed";
    public string MessageOfTheDay { get; set; } =
        "Welcome to NexusUnleashed - a clean-room WildStar realm. Open to all, owned by none.";

    public string BindAddress { get; set; } = "0.0.0.0";
    public int StsPort { get; set; } = 6600;    // UNPINNED: confirm from oracle/launcher config
    public int AuthPort { get; set; } = 23115;   // UNPINNED: real ports from oracle
    public int WorldPort { get; set; } = 24000;
    public string Database { get; set; } = "Server=127.0.0.1;Port=3307;User=root;Database=worlddb";
    /// <summary>authdb connection; when empty, the host uses the in-memory dev store.</summary>
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
