// NexusUnleashed - clean-room authored. Vector helpers carrying the frozen
// realm's hard-won safety LAWS as code:
//   * Normalize of a zero (or near-zero) vector must NOT produce NaN. Math.Clamp
//     does not stop NaN - comparisons against NaN are all false - so guard the
//     magnitude explicitly. (This was the mobs-vanish/under-the-map bug.)
//   * Any position handed to the sim must be finite; a non-finite result is
//     rejected at the source, never propagated.
using System;
using System.Numerics;

namespace NexusUnleashed.World;

public static class Vec
{
    public static bool IsFinite(Vector3 v)
        => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    /// <summary>Normalize, or return Zero for a (near-)zero vector. Never NaN.</summary>
    public static Vector3 SafeNormalize(Vector3 v)
    {
        float len = v.Length();
        if (!float.IsFinite(len) || len < 1e-6f) return Vector3.Zero;
        return v / len;
    }

    /// <summary>Horizontal (X/Z) distance - vision/leash use this alongside 3D.</summary>
    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
