// Content-loader proof against the REAL exported realm data:
// counts must equal the live DB's own counts (measured at export time).
using System;
using System.Linq;
using NexusUnleashed.Content;

string root = args.Length > 0 ? args[0] : "../../content";
var sw = System.Diagnostics.Stopwatch.StartNew();
WorldContent c = WorldContent.Load(root);
sw.Stop();

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"  PASS {name} {detail}"); }
    else { fail++; Console.WriteLine($"  FAIL {name} {detail}"); }
}

Check("spawns == live DB count", c.Spawns.Count == 263756, $"({c.Spawns.Count})");
Check("patrol wires", c.Patrols.Count == 8059, $"({c.Patrols.Count})");
Check("kit entries", c.Kits.Values.Sum(k => k.Count) == 20020, $"({c.Kits.Values.Sum(k => k.Count)})");
Check("kit creatures", c.Kits.Count > 3000, $"({c.Kits.Count})");

// spot checks: a known spawn world and a known kit
var worlds = c.SpawnsByWorld.Select(g => g.Key).ToHashSet();
Check("world 990 populated", worlds.Contains(990u), $"({c.SpawnsByWorld[990u].Count()} spawns)");
Check("world 3335 (Arcterra) populated", worlds.Contains(3335u), $"({c.SpawnsByWorld[3335u].Count()} spawns)");
Check("Firestorm Bomber kit", c.Kits.TryGetValue(27402u, out var kit) && kit.Any(k => k.Spell4Id == 55766),
    kit != null ? $"({kit.Count} spells)" : "");
Check("no zero-position flood", c.Spawns.Count(s => s.X == 0 && s.Y == 0 && s.Z == 0) < 50,
    $"({c.Spawns.Count(s => s.X == 0 && s.Y == 0 && s.Z == 0)} at origin)");

Console.WriteLine($"loaded in {sw.ElapsedMilliseconds} ms | worlds: {worlds.Count}");
Console.WriteLine($"{pass} pass / {fail} fail");
return fail == 0 ? 0 : 1;
