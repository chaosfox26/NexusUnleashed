namespace NexusUnleashed.World;

public interface ITerrainProvider
{
    float? HeightAt(uint worldId, float x, float z);
}

public sealed class NullTerrain : ITerrainProvider
{
    public float? HeightAt(uint worldId, float x, float z) => null;
}
