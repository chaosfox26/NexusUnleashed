// Proof: the GENERATED typed models load real client tables with typed field
// access, and counts match the known table sizes.
using System;
using System.Linq;
using NexusUnleashed.GameData.Generated;

string tblDir = args.Length > 0 ? args[0] : ".";
if (args.Length > 1 && args[1] == "--all") return ReadAll.Run(args[0]);
if (args.Length > 1 && args[1] == "--dump") return Dump.Run(args[0]);
if (args.Length > 1 && args[1] == "--service") return DataServiceTest.Run(args[0]);

string P(string t) => System.IO.Path.Combine(tblDir, t + ".tbl");

int pass = 0, fail = 0;
void Check(string name, bool ok, string d = "")
{ if (ok) { pass++; Console.WriteLine($"  PASS {name} {d}"); } else { fail++; Console.WriteLine($"  FAIL {name} {d}"); } }

var creatures = Creature2Table.Load(P("Creature2"));
Check("Creature2 row count", creatures.Count == 53137, $"({creatures.Count})");
Check("Creature2 typed field access (ID ascending-ish, nonzero)", creatures.Any(c => c.ID > 0));
var byId = creatures.ToDictionary(c => c.ID, c => c);
Check("Creature2 IDs unique", byId.Count == creatures.Count, $"({byId.Count})");

var spells = Spell4Table.Load(P("Spell4"));
Check("Spell4 row count", spells.Count == 66383, $"({spells.Count})");

var worlds = WorldTable.Load(P("World"));
Check("World has rows", worlds.Count > 0, $"({worlds.Count})");
// world 990 (Everstar) and 3335 (Arcterra) exist in the client table
var wids = worlds.Select(w => w.ID).ToHashSet();
Check("world 990 present", wids.Contains(990u));
Check("world 3335 present", wids.Contains(3335u));

var quests = Quest2Table.Load(P("Quest2"));
Check("Quest2 has rows", quests.Count > 0, $"({quests.Count})");

Console.WriteLine($"{pass} pass / {fail} fail");
return fail == 0 ? 0 : 1;
