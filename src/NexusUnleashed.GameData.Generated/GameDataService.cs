// NexusUnleashed - clean-room authored. The game-data service: loads the core
// client tables + localization once and exposes typed lookups for the world
// layer (spawning needs Creature2, casting needs Spell4, etc.). All data is
// Carbine's; this is the query surface over it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NexusUnleashed.GameData;

namespace NexusUnleashed.GameData.Generated;

public sealed class GameDataService
{
    private readonly string _tblDir;

    public IReadOnlyDictionary<uint, Creature2Entry> Creatures { get; private set; } = new Dictionary<uint, Creature2Entry>();
    public IReadOnlyDictionary<uint, Spell4Entry> Spells { get; private set; } = new Dictionary<uint, Spell4Entry>();
    public IReadOnlyDictionary<uint, WorldEntry> Worlds { get; private set; } = new Dictionary<uint, WorldEntry>();
    public IReadOnlyDictionary<uint, Quest2Entry> Quests { get; private set; } = new Dictionary<uint, Quest2Entry>();
    public IReadOnlyDictionary<uint, string> Text { get; private set; } = new Dictionary<uint, string>();

    public GameDataService(string tblDir) => _tblDir = tblDir;

    public void Load()
    {
        string P(string t) => Path.Combine(_tblDir, t + ".tbl");
        Creatures = Creature2Table.Load(P("Creature2")).ToDictionary(c => c.ID);
        Spells    = Spell4Table.Load(P("Spell4")).ToDictionary(s => s.ID);
        Worlds    = WorldTable.Load(P("World")).ToDictionary(w => w.ID);
        Quests    = Quest2Table.Load(P("Quest2")).ToDictionary(q => q.ID);

        string bin = Path.Combine(_tblDir, "en-US.bin");
        if (File.Exists(bin)) Text = TextTable.Read(bin);
    }

    /// <summary>Localized display name of a creature, or "" if unknown.</summary>
    public string CreatureName(uint creatureId)
        => Creatures.TryGetValue(creatureId, out var c) && Text.TryGetValue(c.LocalizedTextIdName, out var n) ? n : "";

    public string TextOf(uint textId) => Text.TryGetValue(textId, out var s) ? s : "";
}
