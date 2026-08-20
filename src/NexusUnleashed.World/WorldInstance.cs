using System;
using System.Collections.Generic;
using System.Numerics;

namespace NexusUnleashed.World;

public sealed class VisionDelta
{
    public List<uint> Added { get; } = new();
    public List<uint> Removed { get; } = new();
    public bool Any => Added.Count > 0 || Removed.Count > 0;
}

public sealed class WorldInstance
{
    public uint WorldId { get; }

    private readonly Dictionary<uint, Entity> _entities = new();
    private readonly SpatialGrid _grid;
    private uint _nextGuid = 1;

    public float VisionEnter { get; }
    public float VisionLeave { get; }

    public WorldInstance(uint worldId, float cellSize = 128f, float visionEnter = 128f, float visionLeave = 141f)
    {
        WorldId = worldId;
        _grid = new SpatialGrid(cellSize);
        VisionEnter = visionEnter;
        VisionLeave = visionLeave;
    }

    public int EntityCount => _entities.Count;
    public IReadOnlyDictionary<uint, Entity> Entities => _entities;

    public uint Add(Entity e)
    {
        uint guid = _nextGuid++;
        e.Guid = guid;
        _entities[guid] = e;
        _grid.Add(guid, e.Position);
        return guid;
    }

    public void Move(uint guid, Vector3 pos)
    {
        if (!_entities.TryGetValue(guid, out var e)) return;
        e.Position = pos;
        _grid.Move(guid, pos);
    }

    public void Remove(uint guid)
    {
        if (_entities.Remove(guid)) _grid.Remove(guid);
    }

    public VisionDelta UpdateVision(PlayerEntity viewer)
    {
        var delta = new VisionDelta();
        var candidates = new List<uint>();
        _grid.QueryNeighborhood(viewer.Position, VisionLeave, candidates);

        var stillVisible = new HashSet<uint>();
        foreach (uint guid in candidates)
        {
            if (guid == viewer.Guid) continue;
            if (!_entities.TryGetValue(guid, out var e)) continue;

            float dist = Vector3.Distance(viewer.Position, e.Position);
            bool wasVisible = viewer.Visible.Contains(guid);

            bool visible = wasVisible ? dist <= VisionLeave                                      : dist <= VisionEnter;            if (visible)
            {
                stillVisible.Add(guid);
                if (!wasVisible) delta.Added.Add(guid);
            }
        }

        foreach (uint guid in viewer.Visible)
            if (!stillVisible.Contains(guid))
                delta.Removed.Add(guid);

        viewer.Visible.Clear();
        foreach (uint g in stillVisible) viewer.Visible.Add(g);
        return delta;
    }
}
