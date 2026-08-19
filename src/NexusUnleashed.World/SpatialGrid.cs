// NexusUnleashed - clean-room authored. A uniform spatial hash over the X/Z
// plane (Y is up) for interest management: which entities are near a point.
// Cell size is chosen so a vision radius spans a small, fixed neighborhood -
// the classic grid partition every MMO uses; the shape is standard, not copied.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace NexusUnleashed.World;

public sealed class SpatialGrid
{
    private readonly float _cellSize;
    private readonly Dictionary<long, HashSet<uint>> _cells = new();
    private readonly Dictionary<uint, long> _entityCell = new();

    public SpatialGrid(float cellSize = 128f) => _cellSize = cellSize;

    public int CellCount => _cells.Count;

    private long Key(Vector3 p)
    {
        int cx = (int)MathF.Floor(p.X / _cellSize);
        int cz = (int)MathF.Floor(p.Z / _cellSize);
        return ((long)cx << 32) ^ (uint)cz;
    }

    public void Add(uint guid, Vector3 pos)
    {
        long key = Key(pos);
        if (!_cells.TryGetValue(key, out var set)) _cells[key] = set = new HashSet<uint>();
        set.Add(guid);
        _entityCell[guid] = key;
    }

    public void Move(uint guid, Vector3 newPos)
    {
        long newKey = Key(newPos);
        if (_entityCell.TryGetValue(guid, out long oldKey))
        {
            if (oldKey == newKey) return;
            if (_cells.TryGetValue(oldKey, out var oldSet))
            {
                oldSet.Remove(guid);
                if (oldSet.Count == 0) _cells.Remove(oldKey);
            }
        }
        if (!_cells.TryGetValue(newKey, out var set)) _cells[newKey] = set = new HashSet<uint>();
        set.Add(guid);
        _entityCell[guid] = newKey;
    }

    public void Remove(uint guid)
    {
        if (_entityCell.TryGetValue(guid, out long key))
        {
            if (_cells.TryGetValue(key, out var set))
            {
                set.Remove(guid);
                if (set.Count == 0) _cells.Remove(key);
            }
            _entityCell.Remove(guid);
        }
    }

    /// <summary>
    /// All entity guids whose CELL is within the neighborhood covering `radius`
    /// around `center`. A coarse pre-filter - the caller does the exact distance
    /// test. Never misses an in-range entity (that was the frozen realm's vanish
    /// bug: a search that missed an entity plainly inside range).
    /// </summary>
    public void QueryNeighborhood(Vector3 center, float radius, List<uint> outGuids)
    {
        outGuids.Clear();
        int span = (int)MathF.Ceiling(radius / _cellSize);
        int ccx = (int)MathF.Floor(center.X / _cellSize);
        int ccz = (int)MathF.Floor(center.Z / _cellSize);
        for (int dx = -span; dx <= span; dx++)
        for (int dz = -span; dz <= span; dz++)
        {
            long key = ((long)(ccx + dx) << 32) ^ (uint)(ccz + dz);
            if (_cells.TryGetValue(key, out var set))
                foreach (uint g in set) outGuids.Add(g);
        }
    }
}
