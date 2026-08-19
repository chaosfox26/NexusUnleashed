// NexusUnleashed - clean-room authored. Holds every world resident at once and
// ticks them. This is the operator's target: the whole game loaded
// simultaneously (the frozen realm's sweep-on-boot behavior), not one map at a
// time on player demand. Worlds with no spawns are still resident and ready.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NexusUnleashed.World;

public sealed class WorldManager
{
    private readonly ConcurrentDictionary<uint, WorldInstance> _worlds = new();

    public int WorldCount => _worlds.Count;
    public IReadOnlyCollection<WorldInstance> Worlds => (IReadOnlyCollection<WorldInstance>)_worlds.Values;

    public WorldInstance GetOrCreate(uint worldId)
        => _worlds.GetOrAdd(worldId, id => new WorldInstance(id));

    public bool TryGet(uint worldId, out WorldInstance world) => _worlds.TryGetValue(worldId, out world!);

    /// <summary>Bring a set of worlds resident (empty but ready). Parallel.</summary>
    public void LoadAll(IEnumerable<uint> worldIds)
    {
        Parallel.ForEach(worldIds, id => _worlds.TryAdd(id, new WorldInstance(id)));
    }

    public long TotalEntities()
    {
        long n = 0;
        foreach (var w in _worlds.Values) n += w.EntityCount;
        return n;
    }

    /// <summary>
    /// One simulation tick across every resident world, in parallel. `perWorld`
    /// runs for each world (movement, vision, AI wire into here).
    /// </summary>
    public void Tick(float dt, Action<WorldInstance, float> perWorld)
    {
        Parallel.ForEach(_worlds.Values, w => perWorld(w, dt));
    }
}
