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

    public void Tick(float dt, Action<WorldInstance, float> perWorld)
    {
        Parallel.ForEach(_worlds.Values, w => perWorld(w, dt));
    }
}
