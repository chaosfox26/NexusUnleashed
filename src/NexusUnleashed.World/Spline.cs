// NexusUnleashed - clean-room authored. Catmull-Rom spline paths for patrols,
// carrying the frozen realm's spline LAWS as code:
//   * A spline needs >= 4 nodes. Fewer is rejected at construction (the engine's
//     spline types require it; patrol passes enforced it).
//   * Evaluation is bounded and finite-guarded. The realm crashed with a NaN
//     recursion stack overflow (0xC00000FD) in a recursive Catmull-Rom; here the
//     arc-length walk is ITERATIVE with a hard step cap and every sample is
//     IsFinite-checked, so a degenerate node run can never blow the stack or
//     emit NaN.
//   * An exotic/unknown spline mode falls back to a warned one-shot, never throws.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace NexusUnleashed.World;

public enum SplineMode { OneShot, Loop, PingPong }

public sealed class CatmullRomSpline
{
    public const int MinNodes = 4;
    private readonly Vector3[] _nodes;

    private CatmullRomSpline(Vector3[] nodes) => _nodes = nodes;

    /// <summary>Build a spline, or null if there are fewer than 4 finite nodes.</summary>
    public static CatmullRomSpline? TryCreate(IReadOnlyList<Vector3> nodes)
    {
        if (nodes == null || nodes.Count < MinNodes) return null;
        var arr = new Vector3[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            if (!Vec.IsFinite(nodes[i])) return null;
            arr[i] = nodes[i];
        }
        return new CatmullRomSpline(arr);
    }

    public int SegmentCount => _nodes.Length - 3;

    /// <summary>Evaluate segment s (0..SegmentCount-1) at local u in [0,1].</summary>
    public Vector3 Evaluate(int s, float u)
    {
        // clamp indices; Catmull-Rom uses p0..p3 around the segment p1->p2
        s = Math.Clamp(s, 0, SegmentCount - 1);
        Vector3 p0 = _nodes[s], p1 = _nodes[s + 1], p2 = _nodes[s + 2], p3 = _nodes[s + 3];
        float u2 = u * u, u3 = u2 * u;
        Vector3 r = 0.5f * (
            2f * p1 +
            (-p0 + p2) * u +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        return Vec.IsFinite(r) ? r : p1;   // LAW: never emit NaN
    }

    /// <summary>Total path length, sampled iteratively (bounded, finite-guarded).</summary>
    public float Length(int samplesPerSegment = 16)
    {
        float len = 0; Vector3 prev = Evaluate(0, 0f);
        int total = SegmentCount * samplesPerSegment;
        for (int i = 1; i <= total; i++)
        {
            int s = i / samplesPerSegment;
            float u = (i % samplesPerSegment) / (float)samplesPerSegment;
            Vector3 p = Evaluate(Math.Min(s, SegmentCount - 1), u);
            float d = Vector3.Distance(prev, p);
            if (float.IsFinite(d)) len += d;
            prev = p;
        }
        return len;
    }
}

/// <summary>Walks an entity along a spline at a fixed speed.</summary>
public sealed class SplineFollower : IMovementGenerator
{
    private readonly CatmullRomSpline _spline;
    private readonly float _speed;
    private readonly SplineMode _mode;
    private float _t;          // 0..SegmentCount
    private int _dir = 1;
    public bool Done { get; private set; }

    public SplineFollower(CatmullRomSpline spline, float speed, SplineMode mode = SplineMode.Loop)
    {
        _spline = spline; _speed = speed; _mode = mode;
    }

    public Vector3 Next(Vector3 current, float dt)
    {
        // advance parameter by an approximate segments-per-second; the exact
        // arc-length reparam is bounded and finite - no recursion.
        float segLen = MathF.Max(_spline.Length() / MathF.Max(_spline.SegmentCount, 1), 1e-3f);
        _t += _dir * (_speed * dt / segLen);

        if (_t >= _spline.SegmentCount)
        {
            switch (_mode)
            {
                case SplineMode.Loop: _t -= _spline.SegmentCount; break;
                case SplineMode.PingPong: _t = _spline.SegmentCount - (_t - _spline.SegmentCount); _dir = -1; break;
                default: _t = _spline.SegmentCount; Done = true; break;
            }
        }
        else if (_t < 0)
        {
            _t = -_t; _dir = 1;   // ping-pong bounce at the start
        }

        int seg = Math.Clamp((int)_t, 0, _spline.SegmentCount - 1);
        float u = _t - seg;
        return _spline.Evaluate(seg, u);
    }
}
