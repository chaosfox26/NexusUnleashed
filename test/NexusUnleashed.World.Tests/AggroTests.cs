using System;
using System.Collections.Generic;
using System.Numerics;
using NexusUnleashed.GameData.Generated;
using NexusUnleashed.World;

static class AggroTests
{
    public static int Run(string tblDir)
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        string P(string t) => System.IO.Path.Combine(tblDir, t + ".tbl");
        var factions = Faction2Table.Load(P("Faction2"));
        var rels = Faction2RelationshipTable.Load(P("Faction2Relationship"));
        var fs = new FactionSystem(factions, rels);

        Check("faction 219 -> 165 is level 10 (client fact)", fs.LevelBetween(219, 165) == 10, $"({fs.LevelBetween(219,165)})");
        Check("219 is NOT hostile to 165 (Beloved, per client)", !fs.IsHostile(219, 165));

        uint hf0 = 0, hf1 = 0;
        foreach (var r in rels) if (r.FactionLevel == 0) { hf0 = r.FactionId0; hf1 = r.FactionId1; break; }
        Check("a level-0 relationship reads hostile", hf0 != 0 && fs.IsHostile(hf0, hf1), $"({hf0}->{hf1})");

        var playerFaction = hf1;
        var home = new Vector3(0, -919, 0);
        var hostileAI = new CreatureAI(home, faction: hf0, isAggressive: false, isRooted: false, aggroRadius: 20f, leashRadius: 40f);
        var playersFar = new List<(uint, Vector3, uint)> { (1u, new Vector3(0, -919, 30), playerFaction) };
        var playersNear = new List<(uint, Vector3, uint)> { (1u, new Vector3(0, -919, 10), playerFaction) };

        Check("hostile creature ignores player out of aggro range", hostileAI.Update(home, playersFar, fs) == 0 && hostileAI.State == AggroState.Idle);
        Check("hostile creature aggros player in range", hostileAI.Update(home, playersNear, fs) == 1u && hostileAI.State == AggroState.Pursuing);

        var neutralAI = new CreatureAI(home, faction: 219, isAggressive: false, isRooted: false, aggroRadius: 20f);
        var neutralPlayers = new List<(uint, Vector3, uint)> { (2u, new Vector3(0, -919, 5), 165u) };        Check("neutral non-aggressive wildlife does NOT aggro (Mystpaw law)", neutralAI.Update(home, neutralPlayers, fs) == 0 && neutralAI.State == AggroState.Idle);

        var aggroAI = new CreatureAI(home, faction: 219, isAggressive: true, isRooted: false, aggroRadius: 20f);
        Check("aggressive creature aggros even a non-hostile player", aggroAI.Update(home, neutralPlayers, fs) == 2u && aggroAI.State == AggroState.Pursuing);

        var rootedAI = new CreatureAI(home, faction: hf0, isAggressive: false, isRooted: true, aggroRadius: 20f);
        uint rootedMove = rootedAI.Update(home, playersNear, fs);
        Check("rooted creature engages but does not chase (faces only)", rootedMove == 0u && rootedAI.State == AggroState.Pursuing);

        var chaser = new CreatureAI(home, faction: hf0, isAggressive: false, isRooted: false, aggroRadius: 20f, leashRadius: 40f);
        chaser.Update(home, playersNear, fs);        var runaway = new List<(uint, Vector3, uint)> { (1u, new Vector3(0, -919, 100), playerFaction) };
        Check("creature leashes and returns when target passes leash", chaser.Update(home, runaway, fs) == 0u && chaser.State == AggroState.Returning);

        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
