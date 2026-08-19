// NexusUnleashed - clean-room authored. The base world entity. Position uses
// the client's coordinate convention (X east, Y up, Z north) - a fact from the
// realm's own data (entity.x/y/z, worlddb). Ids are server-assigned.
using System.Collections.Generic;
using System.Numerics;

namespace NexusUnleashed.World;

public enum EntityKind : byte
{
    // Values mirror the worlddb `entity.type` tinyint (a fact from our data):
    // 0 = Simple (props/interactables), other values are creature-ish. We keep
    // the raw byte and expose the ones the sim needs.
    Simple = 0,
    Creature = 3,
    Player = 20,
}

public class Entity
{
    /// <summary>Server-assigned world guid (unique within a WorldInstance).</summary>
    public uint Guid { get; internal set; }

    /// <summary>Creature2 id (0 for players / pure props).</summary>
    public uint CreatureId { get; init; }

    public byte RawType { get; init; }
    public Vector3 Position { get; set; }
    public float Facing { get; set; }           // radians, worlddb entity.ry
    public uint Faction { get; init; }
    public uint DisplayInfo { get; init; }

    /// <summary>Vision radius in world units. Players see; most props don't.</summary>
    public virtual float VisionRange => 0f;

    public bool IsPlayer => RawType == (byte)EntityKind.Player;

    public override string ToString()
        => $"#{Guid} creature={CreatureId} type={RawType} @({Position.X:F1},{Position.Y:F1},{Position.Z:F1})";
}

/// <summary>A viewer (player) maintains a set of entities currently in vision.</summary>
public sealed class PlayerEntity : Entity
{
    public const float DefaultVisionRange = 128f;
    public override float VisionRange => DefaultVisionRange;

    /// <summary>Guids currently visible to this player (interest set).</summary>
    public HashSet<uint> Visible { get; } = new();
}
