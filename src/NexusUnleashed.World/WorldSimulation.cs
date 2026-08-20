using System;
using System.Collections.Generic;
using System.Numerics;

namespace NexusUnleashed.World;

public sealed class CreatureSimState
{
    public required IMovementGenerator Wander { get; init; }
    public required CreatureAI Ai { get; init; }
}

public sealed class WorldSimulation
{
    private readonly WorldInstance _world;
    private readonly MovementManager _movement;
    private readonly FactionSystem _factions;
    private readonly Dictionary<uint, CreatureSimState> _creatures = new();

    public WorldSimulation(WorldInstance world, FactionSystem factions, ITerrainProvider? terrain = null)
    {
        _world = world;
        _factions = factions;
        _movement = new MovementManager(world, terrain);
    }

    public WorldInstance World => _world;

    public void Register(uint guid, CreatureSimState state) => _creatures[guid] = state;

    public void Tick(float dt)
    {
        var players = new List<(uint Guid, Vector3 Pos, uint Faction)>();
        foreach (var kv in _world.Entities)
            if (kv.Value is PlayerEntity pe)
                players.Add((pe.Guid, pe.Position, pe.Faction));

        foreach (var (guid, st) in _creatures)
        {
            if (!_world.Entities.TryGetValue(guid, out var e)) continue;
            uint chase = st.Ai.Update(e.Position, players, _factions);
            if (chase != 0 && _world.Entities.TryGetValue(chase, out var target))
            {
                var dir = Vec.SafeNormalize(new Vector3(target.Position.X - e.Position.X, 0, target.Position.Z - e.Position.Z));
                var next = e.Position + dir * (6f * dt);
                if (Vec.IsFinite(next)) _world.Move(guid, next);
            }
            else if (st.Ai.State == AggroState.Idle)
            {
                _movement.Step(guid, st.Wander, dt);            }
            else if (st.Ai.State == AggroState.Returning)
            {
                var dir = Vec.SafeNormalize(new Vector3(st.Ai.Home.X - e.Position.X, 0, st.Ai.Home.Z - e.Position.Z));
                var next = e.Position + dir * (6f * dt);
                if (Vec.IsFinite(next)) _world.Move(guid, next);
            }
        }

        foreach (var kv in _world.Entities)
            if (kv.Value is PlayerEntity pe)
                _world.UpdateVision(pe);
    }
}
