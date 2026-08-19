// NexusUnleashed - clean-room authored. One running world (map): its entities,
// the spatial grid, and interest management. Vision uses HYSTERESIS - the frozen
// realm's own proven fix for the vanish/flicker bug (enter at 128, leave at
// ~141): an entity becomes visible inside the enter radius and stays visible
// until it passes the larger leave radius, so entities hovering at the edge do
// not churn in and out. That behavior is a measured fact about our realm, not
// copied code.
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

    // Interest thresholds. Enter/leave differ by design (hysteresis).
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

    /// <summary>
    /// Recompute a player's visible set with hysteresis. Returns which entities
    /// entered and left vision this pass (the wire layer sends creates/destroys
    /// from this). Never drops an entity that is still inside the leave radius -
    /// the bug that made mobs vanish while plainly in range.
    /// </summary>
    public VisionDelta UpdateVision(PlayerEntity viewer)
    {
        var delta = new VisionDelta();
        var candidates = new List<uint>();
        // pre-filter with the LARGER (leave) radius so currently-visible edge
        // entities are always reconsidered, never silently dropped.
        _grid.QueryNeighborhood(viewer.Position, VisionLeave, candidates);

        var stillVisible = new HashSet<uint>();
        foreach (uint guid in candidates)
        {
            if (guid == viewer.Guid) continue;
            if (!_entities.TryGetValue(guid, out var e)) continue;

            float dist = Vector3.Distance(viewer.Position, e.Position);
            bool wasVisible = viewer.Visible.Contains(guid);

            bool visible = wasVisible ? dist <= VisionLeave    // stay until leave radius
                                      : dist <= VisionEnter;   // appear at enter radius
            if (visible)
            {
                stillVisible.Add(guid);
                if (!wasVisible) delta.Added.Add(guid);
            }
        }

        // anything previously visible but no longer a candidate / in range leaves
        foreach (uint guid in viewer.Visible)
            if (!stillVisible.Contains(guid))
                delta.Removed.Add(guid);

        viewer.Visible.Clear();
        foreach (uint g in stillVisible) viewer.Visible.Add(g);
        return delta;
    }
}
