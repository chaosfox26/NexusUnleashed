using System.Collections.Generic;
using System.Numerics;

namespace NexusUnleashed.World;

public enum EntityKind : byte
{
    Simple = 0,
    Creature = 3,
    Player = 20,
}

public class Entity
{
    public uint Guid { get; internal set; }

    public uint CreatureId { get; init; }

    public byte RawType { get; init; }
    public Vector3 Position { get; set; }
    public float Facing { get; set; }    public uint Faction { get; init; }
    public uint DisplayInfo { get; init; }

    public virtual float VisionRange => 0f;

    public bool IsPlayer => RawType == (byte)EntityKind.Player;

    public override string ToString()
        => $"#{Guid} creature={CreatureId} type={RawType} @({Position.X:F1},{Position.Y:F1},{Position.Z:F1})";
}

public sealed class PlayerEntity : Entity
{
    public const float DefaultVisionRange = 128f;
    public override float VisionRange => DefaultVisionRange;

    public HashSet<uint> Visible { get; } = new();
}
