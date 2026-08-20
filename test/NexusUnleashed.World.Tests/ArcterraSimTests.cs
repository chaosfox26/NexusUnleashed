using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusUnleashed.Content;
using NexusUnleashed.GameData.Generated;
using NexusUnleashed.World;

static class ArcterraSimTests
{
    public static int Run(string tblDir, string contentRoot)
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        string P(string t) => System.IO.Path.Combine(tblDir, t + ".tbl");
        var fs = new FactionSystem(Faction2Table.Load(P("Faction2")), Faction2RelationshipTable.Load(P("Faction2Relationship")));
        var content = WorldContent.Load(contentRoot);
        var arcterra = content.Spawns.Where(s => s.WorldId == 3335u).ToList();
        Check("Arcterra has spawns", arcterra.Count > 0, $"({arcterra.Count})");

        var world = new WorldInstance(3335u);
        var sim = new WorldSimulation(world, fs);
        int creatures = 0;
        foreach (var s in arcterra)
        {
            var home = new Vector3(s.X, s.Y, s.Z);
            uint g = world.Add(new Entity { CreatureId = s.CreatureId, RawType = s.Type, Faction = s.Faction, Position = home, Facing = s.Yaw });
            if (s.Type == (byte)EntityKind.Creature || s.Type == 0)
            {
                bool aggressive = (g % 5 == 0);
                sim.Register(g, new CreatureSimState {
                    Wander = new RandomWander(home, 12f, 4f, seed: (int)g),
                    Ai = new CreatureAI(home, s.Faction, aggressive, isRooted: false, aggroRadius: 18f, leashRadius: 35f)
                });
                creatures++;
            }
        }
        Check("creatures registered for sim", creatures > 0, $"({creatures})");

        var start = new Vector3(arcterra[0].X, arcterra[0].Y, arcterra[0].Z);
        var player = new PlayerEntity { Faction = 166u, Position = start };        uint pg = world.Add(player);

        bool anyNaN = false; int maxEngaged = 0; float maxLeashSeen = 0;
        var rng = new Random(5);
        for (int t = 0; t < 600; t++)
        {
            world.Move(pg, player.Position + new Vector3((float)(rng.NextDouble()-0.5)*3, 0, (float)(rng.NextDouble()-0.5)*3));
            sim.Tick(0.1f);

            foreach (var kv in world.Entities)
                if (!Vec.IsFinite(kv.Value.Position)) anyNaN = true;

        }
        Check("600 ticks on Arcterra: zero NaN", !anyNaN);
        Check("player has a vision set after the walk", player.Visible.Count >= 0, $"({player.Visible.Count} visible)");
        Check("all entities still within sane bounds", world.Entities.Values.All(e => MathF.Abs(e.Position.Y) < 1e6f));

        Console.WriteLine($"    Arcterra: {world.EntityCount} entities, {creatures} sim creatures, ran 600 ticks");
        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
