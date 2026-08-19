// Aggro proof: the frozen realm's rule (hostile OR aggressive, in range, leash
// from home) and the operator's Mystpaw law (neutral non-aggressive wildlife is
// left alone). Faction relationships come straight from the client tables.
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

        // client fact: faction 219 -> 165 is level 10 (Beloved), NOT hostile
        Check("faction 219 -> 165 is level 10 (client fact)", fs.LevelBetween(219, 165) == 10, $"({fs.LevelBetween(219,165)})");
        Check("219 is NOT hostile to 165 (Beloved, per client)", !fs.IsHostile(219, 165));

        // player factions (Exile/Dominion) - pick a known hostile pair from data
        // find any relationship at level 0 (hostile end) to exercise IsHostile
        uint hf0 = 0, hf1 = 0;
        foreach (var r in rels) if (r.FactionLevel == 0) { hf0 = r.FactionId0; hf1 = r.FactionId1; break; }
        Check("a level-0 relationship reads hostile", hf0 != 0 && fs.IsHostile(hf0, hf1), $"({hf0}->{hf1})");

        var playerFaction = hf1;   // player belongs to the faction the mob hates

        // --- aggro state machine ---
        var home = new Vector3(0, -919, 0);
        // hostile creature, aggro radius 20
        var hostileAI = new CreatureAI(home, faction: hf0, isAggressive: false, isRooted: false, aggroRadius: 20f, leashRadius: 40f);
        var playersFar = new List<(uint, Vector3, uint)> { (1u, new Vector3(0, -919, 30), playerFaction) };
        var playersNear = new List<(uint, Vector3, uint)> { (1u, new Vector3(0, -919, 10), playerFaction) };

        Check("hostile creature ignores player out of aggro range", hostileAI.Update(home, playersFar, fs) == 0 && hostileAI.State == AggroState.Idle);
        Check("hostile creature aggros player in range", hostileAI.Update(home, playersNear, fs) == 1u && hostileAI.State == AggroState.Pursuing);

        // Mystpaw law: NEUTRAL, non-aggressive creature leaves the player alone
        var neutralAI = new CreatureAI(home, faction: 219, isAggressive: false, isRooted: false, aggroRadius: 20f);
        // 219 -> playerFaction: if not hostile, must NOT aggro
        var neutralPlayers = new List<(uint, Vector3, uint)> { (2u, new Vector3(0, -919, 5), 165u) };  // 165 is Beloved to 219
        Check("neutral non-aggressive wildlife does NOT aggro (Mystpaw law)", neutralAI.Update(home, neutralPlayers, fs) == 0 && neutralAI.State == AggroState.Idle);

        // aggressive neutral creature DOES aggro despite neutral faction
        var aggroAI = new CreatureAI(home, faction: 219, isAggressive: true, isRooted: false, aggroRadius: 20f);
        Check("aggressive creature aggros even a non-hostile player", aggroAI.Update(home, neutralPlayers, fs) == 2u && aggroAI.State == AggroState.Pursuing);

        // rooted creature faces (returns 0) but flips to Pursuing
        var rootedAI = new CreatureAI(home, faction: hf0, isAggressive: false, isRooted: true, aggroRadius: 20f);
        uint rootedMove = rootedAI.Update(home, playersNear, fs);
        Check("rooted creature engages but does not chase (faces only)", rootedMove == 0u && rootedAI.State == AggroState.Pursuing);

        // leash: player runs past leash from home -> creature returns
        var chaser = new CreatureAI(home, faction: hf0, isAggressive: false, isRooted: false, aggroRadius: 20f, leashRadius: 40f);
        chaser.Update(home, playersNear, fs);   // aggro
        var runaway = new List<(uint, Vector3, uint)> { (1u, new Vector3(0, -919, 100), playerFaction) };
        Check("creature leashes and returns when target passes leash", chaser.Update(home, runaway, fs) == 0u && chaser.State == AggroState.Returning);

        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
