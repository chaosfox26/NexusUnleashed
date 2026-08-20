using System;
using System.Linq;
using NexusUnleashed.GameData.Generated;

static class DataServiceTest
{
    public static int Run(string tblDir)
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        var gd = new GameDataService(tblDir);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        gd.Load();
        sw.Stop();

        Check("creatures indexed", gd.Creatures.Count == 53137, $"({gd.Creatures.Count})");
        Check("spells indexed", gd.Spells.Count == 66383, $"({gd.Spells.Count})");
        Check("worlds indexed", gd.Worlds.Count == 2729, $"({gd.Worlds.Count})");
        Check("text table loaded", gd.Text.Count > 100000, $"({gd.Text.Count:N0} strings)");

        var withName = gd.Creatures.Values.Where(c => c.LocalizedTextIdName != 0).ToList();
        int resolved = withName.Count(c => gd.TextOf(c.LocalizedTextIdName).Length > 0);
        double frac = withName.Count == 0 ? 0 : (double)resolved / withName.Count;
        Check("named creatures resolve == proven reader", resolved == 49603 && withName.Count == 53131, $"({resolved}/{withName.Count} = {frac:P1})");

        foreach (var id in new uint[] { 27402u, 21729u, 28454u })
        {
            string nm = gd.CreatureName(id);
            Console.WriteLine($"    creature {id} -> \"{nm}\"");
        }
        Check("Firestorm Bomber (27402) has a name", gd.CreatureName(27402).Length > 0, $"\"{gd.CreatureName(27402)}\"");

        Console.WriteLine($"    loaded in {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
