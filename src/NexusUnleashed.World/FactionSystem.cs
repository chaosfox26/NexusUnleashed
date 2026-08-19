// NexusUnleashed - clean-room authored. Faction relationships straight from the
// client's own Faction2Relationship table (factionId0, factionId1, factionLevel)
// with parent inheritance via Faction2.faction2IdParent. factionLevel is the
// client's standing scale: 0 at the hostile end, 10 = Beloved (measured fact -
// faction 219 -> 165 is level 10 Beloved). The RAW client value is authority;
// we never invent a relationship the tables don't state (operator law: do not
// bend faction behaviour, the client's tables decide).
using System.Collections.Generic;
using NexusUnleashed.GameData.Generated;

namespace NexusUnleashed.World;

public sealed class FactionSystem
{
    // (faction0,faction1) -> factionLevel, as the table states it (directional).
    private readonly Dictionary<(uint, uint), uint> _level = new();
    private readonly Dictionary<uint, uint> _parent = new();

    public FactionSystem(IReadOnlyList<Faction2Entry> factions,
                         IReadOnlyList<Faction2RelationshipEntry> relationships)
    {
        foreach (var f in factions) _parent[f.ID] = f.Faction2IdParent;
        foreach (var r in relationships) _level[(r.FactionId0, r.FactionId1)] = r.FactionLevel;
    }

    /// <summary>
    /// The client standing level of `from` toward `to` (0 hostile … 10 Beloved),
    /// walking parent factions when no direct row exists. Null if the tables
    /// state no relationship at all (caller decides the default, but never
    /// fabricates hostility).
    /// </summary>
    public uint? LevelBetween(uint from, uint to)
    {
        uint a = from;
        for (int guard = 0; guard < 32; guard++)
        {
            uint b = to;
            for (int guard2 = 0; guard2 < 32; guard2++)
            {
                if (_level.TryGetValue((a, b), out uint lvl)) return lvl;
                if (!_parent.TryGetValue(b, out uint pb) || pb == 0 || pb == b) break;
                b = pb;
            }
            if (!_parent.TryGetValue(a, out uint pa) || pa == 0 || pa == a) break;
            a = pa;
        }
        return null;
    }

    /// <summary>
    /// Hostile == the lowest standing band the client uses for red/attackable.
    /// factionLevel 0 is the hostile end (10 is Beloved). The threshold is the
    /// client's standing->disposition mapping; kept as a named constant and
    /// confirmable on the wire against the oracle's nameplate colors.
    /// </summary>
    public const uint HostileMaxLevel = 1;   // levels 0..1 read as hostile

    public bool IsHostile(uint from, uint to)
    {
        uint? lvl = LevelBetween(from, to);
        return lvl.HasValue && lvl.Value <= HostileMaxLevel;
    }
}
