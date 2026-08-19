// NexusUnleashed - clean-room authored. Creature aggro state machine. The rule
// is the frozen realm's own, measured: a creature engages a player when
//   (disposition to the player is HOSTILE) OR (the creature is AGGRESSIVE)
// and the player is within aggro range. Rooted creatures face but never chase
// (StaticCreatures fact). Beyond leash, the creature returns home. Neutral,
// non-aggressive wildlife is left alone - the operator's Mystpaw rule: do not
// invent hostility the client's tables don't state.
using System.Collections.Generic;
using System.Numerics;

namespace NexusUnleashed.World;

public enum AggroState { Idle, Pursuing, Returning }

public sealed class CreatureAI
{
    public Vector3 Home { get; }
    public uint Faction { get; }
    public bool IsAggressive { get; }
    public bool IsRooted { get; }
    public float AggroRadius { get; }
    public float LeashRadius { get; }

    public AggroState State { get; private set; } = AggroState.Idle;
    public uint TargetGuid { get; private set; }

    public CreatureAI(Vector3 home, uint faction, bool isAggressive, bool isRooted,
                      float aggroRadius = 20f, float leashRadius = 40f)
    {
        Home = home; Faction = faction; IsAggressive = isAggressive; IsRooted = isRooted;
        AggroRadius = aggroRadius; LeashRadius = leashRadius;
    }

    /// <summary>
    /// Decide the creature's next state given nearby players. Returns the guid to
    /// move toward (0 = hold / face only). The engine wires this to movement.
    /// </summary>
    public uint Update(Vector3 selfPos, IReadOnlyList<(uint Guid, Vector3 Pos, uint Faction)> players,
                       FactionSystem factions)
    {
        switch (State)
        {
            case AggroState.Idle:
            {
                uint best = 0; float bestDist = float.MaxValue;
                foreach (var p in players)
                {
                    float d = Vec.HorizontalDistance(selfPos, p.Pos);
                    if (d > AggroRadius) continue;
                    bool hostile = factions.IsHostile(Faction, p.Faction);
                    if (!(hostile || IsAggressive)) continue;   // the exact rule
                    if (d < bestDist) { bestDist = d; best = p.Guid; }
                }
                if (best != 0) { State = AggroState.Pursuing; TargetGuid = best; return IsRooted ? 0u : best; }
                return 0;
            }
            case AggroState.Pursuing:
            {
                // still valid target?
                foreach (var p in players)
                    if (p.Guid == TargetGuid)
                    {
                        // leash: measured from HOME, not from the creature
                        if (Vec.HorizontalDistance(Home, p.Pos) > LeashRadius || Vec.HorizontalDistance(Home, selfPos) > LeashRadius)
                        { State = AggroState.Returning; TargetGuid = 0; return 0; }
                        return IsRooted ? 0u : p.Guid;   // rooted: face, never chase
                    }
                // target gone from vision
                State = AggroState.Returning; TargetGuid = 0; return 0;
            }
            case AggroState.Returning:
            default:
            {
                if (Vec.HorizontalDistance(selfPos, Home) < 1f) { State = AggroState.Idle; return 0; }
                return 0;   // caller moves it home via a return generator
            }
        }
    }
}
