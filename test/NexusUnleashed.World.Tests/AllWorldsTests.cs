using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using NexusUnleashed.Content;
using NexusUnleashed.GameData.Generated;
using NexusUnleashed.World;

static class AllWorldsTests
{
    public static int Run(string tblDir, string contentRoot)
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        var worldIds = WorldTable.Load(System.IO.Path.Combine(tblDir, "World.tbl")).Select(w => w.ID).ToList();
        Console.WriteLine($"World.tbl declares {worldIds.Count:N0} worlds");

        var mgr = new WorldManager();
        var sw = Stopwatch.StartNew();
        mgr.LoadAll(worldIds);
        sw.Stop();
        Check("every world resident at once", mgr.WorldCount == worldIds.Count, $"({mgr.WorldCount:N0} in {sw.ElapsedMilliseconds} ms)");

        var content = WorldContent.Load(contentRoot);
        foreach (var s in content.Spawns)
        {
            var w = mgr.GetOrCreate(s.WorldId);
            w.Add(new Entity { CreatureId = s.CreatureId, RawType = s.Type, Faction = s.Faction,
                               DisplayInfo = s.DisplayInfo, Facing = s.Yaw,
                               Position = new Vector3(s.X, s.Y, s.Z) });
        }
        Check("all spawns placed across the resident worlds", mgr.TotalEntities() == content.Spawns.Count, $"({mgr.TotalEntities():N0} entities)");

        long before = GC.GetTotalMemory(true);
        sw.Restart();
        int ticks = 10;
        for (int t = 0; t < ticks; t++)
            mgr.Tick(0.1f, (w, dt) =>
            {
                _ = w.EntityCount;
            });
        sw.Stop();
        long after = GC.GetTotalMemory(false);
        Console.WriteLine($"{ticks} global ticks over {mgr.WorldCount:N0} worlds: {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds/(double)ticks:F1} ms/tick), ~{(after)/1024/1024} MB resident");
        Check("global tick touches all worlds under 1s/tick", sw.ElapsedMilliseconds / (double)ticks < 1000);

        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
