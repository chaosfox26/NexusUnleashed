using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NexusUnleashed.Content;

public sealed record SpawnRecord(
    ulong Id, uint CreatureId, uint WorldId,
    float X, float Y, float Z, float Yaw,
    uint DisplayInfo, uint OutfitInfo, uint Faction, byte Type);

public sealed record KitEntry(uint CreatureId, uint Spell4Id, string Label);

public sealed record PatrolWire(ulong EntityId, uint SplineId, uint Mode, float Speed, float Fx, float Fy, float Fz);

public sealed class WorldContent
{
    public List<SpawnRecord> Spawns { get; } = new();
    public Dictionary<uint, List<KitEntry>> Kits { get; } = new();
    public Dictionary<ulong, PatrolWire> Patrols { get; } = new();

    public ILookup<uint, SpawnRecord> SpawnsByWorld => Spawns.ToLookup(s => s.WorldId);

    public static WorldContent Load(string contentRoot)
    {
        var c = new WorldContent();

        string spawns = Path.Combine(contentRoot, "spawns.tsv");
        if (File.Exists(spawns))
        {
            TsvTable t = TsvTable.Read(spawns);
            int id = t.Col("id"), cr = t.Col("creatureId"), w = t.Col("worldId");
            int x = t.Col("x"), y = t.Col("y"), z = t.Col("z"), yaw = t.Col("yaw");
            int di = t.Col("displayInfo"), oi = t.Col("outfitInfo"),
                fa = t.Col("faction"), ty = t.Col("type");
            foreach (var r in t.Rows)
                c.Spawns.Add(new SpawnRecord(
                    TsvValue.U64(r[id]), TsvValue.U32(r[cr]), TsvValue.U32(r[w]),
                    TsvValue.F32(r[x]), TsvValue.F32(r[y]), TsvValue.F32(r[z]),
                    TsvValue.F32(r[yaw]),
                    TsvValue.U32(r[di]), TsvValue.U32(r[oi]),
                    TsvValue.U32(r[fa]), (byte)TsvValue.U32(r[ty])));
        }

        string kits = Path.Combine(contentRoot, "kits.tsv");
        if (File.Exists(kits))
        {
            TsvTable t = TsvTable.Read(kits);
            int cr = t.Col("creatureId"), sp = t.Col("spell4Id");
            int lb = t.HasCol("label") ? t.Col("label") : -1;
            foreach (var r in t.Rows)
            {
                uint creature = TsvValue.U32(r[cr]);
                if (!c.Kits.TryGetValue(creature, out var list))
                    c.Kits[creature] = list = new List<KitEntry>();
                list.Add(new KitEntry(creature, TsvValue.U32(r[sp]), lb >= 0 ? r[lb] : ""));
            }
        }

        string patrols = Path.Combine(contentRoot, "patrols.tsv");
        if (File.Exists(patrols))
        {
            TsvTable t = TsvTable.Read(patrols);
            int en = t.Col("entityId"), sp = t.Col("splineId"), mo = t.Col("mode"),
                spd = t.Col("speed"), fx = t.Col("fx"), fy = t.Col("fy"), fz = t.Col("fz");
            foreach (var r in t.Rows)
            {
                ulong entity = TsvValue.U64(r[en]);
                c.Patrols[entity] = new PatrolWire(entity,
                    TsvValue.U32(r[sp]), TsvValue.U32(r[mo]), TsvValue.F32(r[spd]),
                    TsvValue.F32(r[fx]), TsvValue.F32(r[fy]), TsvValue.F32(r[fz]));
            }
        }

        return c;
    }
}
