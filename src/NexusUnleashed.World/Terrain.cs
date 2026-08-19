// NexusUnleashed - clean-room authored. Terrain height lookup. LAW (frozen
// realm): a failed lookup returns null, NEVER 0 and NEVER NaN. GetValueOrDefault
// on a nullable is 0 on failure, and against a world at Y ~= -919 that threw a
// creature ~919 units skyward; `float?` + `??` guards null, and callers keep the
// entity's current Y on a miss rather than snapping it anywhere.
namespace NexusUnleashed.World;

public interface ITerrainProvider
{
    /// <summary>Ground height at (x,z), or null if unknown. Never 0-on-failure.</summary>
    float? HeightAt(uint worldId, float x, float z);
}

/// <summary>Default: no terrain data -> always null (callers keep current Y).</summary>
public sealed class NullTerrain : ITerrainProvider
{
    public float? HeightAt(uint worldId, float x, float z) => null;
}
