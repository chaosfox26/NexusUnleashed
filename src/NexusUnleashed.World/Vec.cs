using System;
using System.Numerics;

namespace NexusUnleashed.World;

public static class Vec
{
    public static bool IsFinite(Vector3 v)
        => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    public static Vector3 SafeNormalize(Vector3 v)
    {
        float len = v.Length();
        if (!float.IsFinite(len) || len < 1e-6f) return Vector3.Zero;
        return v / len;
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
