using System.Collections.Generic;
using NexusUnleashed.GameData.Generated;

namespace NexusUnleashed.World;

public sealed class FactionSystem
{
    private readonly Dictionary<(uint, uint), uint> _level = new();
    private readonly Dictionary<uint, uint> _parent = new();

    public FactionSystem(IReadOnlyList<Faction2Entry> factions,
                         IReadOnlyList<Faction2RelationshipEntry> relationships)
    {
        foreach (var f in factions) _parent[f.ID] = f.Faction2IdParent;
        foreach (var r in relationships) _level[(r.FactionId0, r.FactionId1)] = r.FactionLevel;
    }

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

    public const uint HostileMaxLevel = 1;
    public bool IsHostile(uint from, uint to)
    {
        uint? lvl = LevelBetween(from, to);
        return lvl.HasValue && lvl.Value <= HostileMaxLevel;
    }
}
