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

    public string CreatureName(uint creatureId)
        => Creatures.TryGetValue(creatureId, out var c) && Text.TryGetValue(c.LocalizedTextIdName, out var n) ? n : "";

    public string TextOf(uint textId) => Text.TryGetValue(textId, out var s) ? s : "";
}
