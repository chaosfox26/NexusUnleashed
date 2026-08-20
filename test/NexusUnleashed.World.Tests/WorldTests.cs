using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using NexusUnleashed.Content;
using NexusUnleashed.World;

string contentRoot = args.Length > 0 ? args[0] : "content";
int pass = 0, fail = 0;
void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

var sw = Stopwatch.StartNew();
WorldContent content = WorldContent.Load(contentRoot);
sw.Stop();
Console.WriteLine($"loaded {content.Spawns.Count:N0} spawns in {sw.ElapsedMilliseconds} ms");

var byWorld = content.Spawns.GroupBy(s => s.WorldId).ToList();
var worlds = new System.Collections.Concurrent.ConcurrentDictionary<uint, WorldInstance>();
sw.Restart();
Parallel.ForEach(byWorld, grp =>
{
    var wi = new WorldInstance(grp.Key);
    foreach (var s in grp)
        wi.Add(new Entity { CreatureId = s.CreatureId, RawType = s.Type, Faction = s.Faction,
                            DisplayInfo = s.DisplayInfo, Facing = s.Yaw,
                            Position = new Vector3(s.X, s.Y, s.Z) });
    worlds[grp.Key] = wi;
});
sw.Stop();
long totalEntities = worlds.Values.Sum(w => (long)w.EntityCount);
Console.WriteLine($"built {worlds.Count} worlds, {totalEntities:N0} entities in {sw.ElapsedMilliseconds} ms ({Environment.ProcessorCount} cores)");

Check("all spawns placed", totalEntities == content.Spawns.Count, $"({totalEntities:N0})");
Check("Everstar 990 present", worlds.ContainsKey(990u) && worlds[990u].EntityCount > 0, $"({(worlds.TryGetValue(990u, out var e990) ? e990.EntityCount : 0)})");
Check("Arcterra 3335 present", worlds.ContainsKey(3335u) && worlds[3335u].EntityCount > 0, $"({(worlds.TryGetValue(3335u, out var e3335) ? e3335.EntityCount : 0)})");

var busiest = worlds.Values.OrderByDescending(w => w.EntityCount).First();
Console.WriteLine($"vision tests on world {busiest.WorldId} ({busiest.EntityCount:N0} entities)");
var rng = new Random(1234);
var ents = busiest.Entities.Values.ToList();
int mismatches = 0, samples = 200;
for (int i = 0; i < samples; i++)
{
    var anchor = ents[rng.Next(ents.Count)].Position;
    var player = new PlayerEntity { Position = anchor };
    busiest.Add(player);
    var delta = busiest.UpdateVision(player);

    var brute = new HashSet<uint>();
    foreach (var kv in busiest.Entities)
        if (kv.Key != player.Guid && Vector3.Distance(anchor, kv.Value.Position) <= player.VisionRange)
            brute.Add(kv.Key);

    if (!player.Visible.SetEquals(brute)) mismatches++;
    busiest.Remove(player.Guid);
}
Check("grid vision == brute force (never misses)", mismatches == 0, $"({samples - mismatches}/{samples} exact)");

{
    var wi = new WorldInstance(999u, visionEnter: 128f, visionLeave: 141f);
    uint near = wi.Add(new Entity { Position = new Vector3(0, 0, 100) });    uint edge = wi.Add(new Entity { Position = new Vector3(0, 0, 135) });    var p = new PlayerEntity { Position = Vector3.Zero };
    wi.Add(p);
    var d1 = wi.UpdateVision(p);
    Check("enters only the in-range entity", p.Visible.Contains(near) && !p.Visible.Contains(edge));
    var d2 = wi.UpdateVision(p);
    Check("edge entity stays hidden (hysteresis, never entered)", !p.Visible.Contains(edge));
    wi.Move(p.Guid, new Vector3(0, 0, 20));    wi.UpdateVision(p);
    Check("edge enters when inside enter radius", p.Visible.Contains(edge));
    wi.Move(p.Guid, Vector3.Zero);    wi.UpdateVision(p);
    Check("edge STAYS visible at 135 once seen (no flicker)", p.Visible.Contains(edge));
    wi.Move(p.Guid, new Vector3(0, 0, -20));    wi.UpdateVision(p);
    Check("edge leaves only past the leave radius", !p.Visible.Contains(edge));
}

sw.Restart();
long visChecks = 0;
Parallel.ForEach(worlds.Values, wi =>
{
    if (wi.EntityCount == 0) return;
    var any = wi.Entities.Values.First().Position;
    var p = new PlayerEntity { Position = any };
    wi.Add(p);
    var d = wi.UpdateVision(p);
    System.Threading.Interlocked.Add(ref visChecks, d.Added.Count);
    wi.Remove(p.Guid);
});
sw.Stop();
Console.WriteLine($"one vision pass per world ({worlds.Count} worlds): {sw.ElapsedMilliseconds} ms, {visChecks:N0} entities entered vision");

Console.WriteLine("-- movement --");
int mv = MovementTests.Run();
Console.WriteLine("-- spline --");
int sp = SplineTests.Run();
Console.WriteLine("-- all worlds resident --");
string tblDir = args.Length > 1 ? args[1] : "assets/tbl";
int aw = AllWorldsTests.Run(tblDir, contentRoot);
Console.WriteLine("-- aggro / faction --");
int ag = AggroTests.Run(tblDir);
Console.WriteLine("-- combat --");
int cb = CombatTests.Run();
Console.WriteLine("-- arcterra living world --");
int arc = ArcterraSimTests.Run(tblDir, contentRoot);
Console.WriteLine($"{pass} pass / {fail} fail (world)");
return (fail == 0 && mv == 0 && sp == 0 && aw == 0 && ag == 0 && cb == 0 && arc == 0) ? 0 : 1;
