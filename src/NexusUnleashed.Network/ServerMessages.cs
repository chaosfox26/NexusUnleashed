// NexusUnleashed - clean-room authored. Typed server->client message models,
// each PINNED from a live capture of the oracle (field layout recovered by
// analyzing thousands of real samples) and validated against real bytes with
// our own PacketReader. The payload as captured is [u16 LE opcode][body]; each
// Parse skips the opcode then reads the body. Field names are ours (inferred
// roles); the layout is a protocol fact from Carbine's wire.
using System;

namespace NexusUnleashed.Network;

public interface IServerMessage
{
    GameMessageOpcode Opcode { get; }
}

/// <summary>0x0355: a small per-entity state update (guid + one flag byte).</summary>
public sealed record ServerEntitySmallUpdate(uint Guid, byte Flag) : IServerMessage
{
    public GameMessageOpcode Opcode => GameMessageOpcode.ServerEntitySmallUpdate;
    public static ServerEntitySmallUpdate Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);                       // opcode
        return new ServerEntitySmallUpdate(r.ReadUInt32(), r.ReadByte());
    }
}

/// <summary>0x0811: apply a spell buff (buffId + stack count + target guid).</summary>
public sealed record ServerSpellBuffAdd(uint BuffId, uint Count, uint TargetGuid) : IServerMessage
{
    public GameMessageOpcode Opcode => (GameMessageOpcode)0x0811;
    public static ServerSpellBuffAdd Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);
        return new ServerSpellBuffAdd(r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32());
    }
}

/// <summary>0x0813: remove a spell buff (buffId + target guid).</summary>
public sealed record ServerSpellBuffRemove(uint BuffId, uint TargetGuid) : IServerMessage
{
    public GameMessageOpcode Opcode => GameMessageOpcode.ServerSpellBuffRemove;
    public static ServerSpellBuffRemove Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);
        return new ServerSpellBuffRemove(r.ReadUInt32(), r.ReadUInt32());
    }
}

/// <summary>
/// 0x0937 / 0x0938: entity update carrying a guid and two 16-bit fields + a
/// trailing byte (exact semantics of the u16s pending; the layout is pinned).
/// </summary>
public sealed record ServerEntityUpdate(ushort Opcode16, uint Guid, ushort FieldA, ushort FieldB, byte Tail) : IServerMessage
{
    public GameMessageOpcode Opcode => (GameMessageOpcode)Opcode16;
    public static ServerEntityUpdate Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        ushort op = (ushort)r.ReadBits(16);
        return new ServerEntityUpdate(op, r.ReadUInt32(), r.ReadUInt16(), r.ReadUInt16(), r.ReadByte());
    }
}

/// <summary>
/// 0x0935: the entity position broadcast (the world heartbeat) - guid + a
/// 4-byte packed movement field + a trailing byte. The movement field's exact
/// decode (position/delta/time) is pending correlation; the framing is pinned
/// and the guid is validated.
/// </summary>
public sealed record ServerEntityPositionUpdate(uint Guid, uint MovementData, byte Tail) : IServerMessage
{
    public GameMessageOpcode Opcode => GameMessageOpcode.ServerEntityPositionUpdate;
    public static ServerEntityPositionUpdate Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);
        return new ServerEntityPositionUpdate(r.ReadUInt32(), r.ReadUInt32(), r.ReadByte());
    }
}

/// <summary>
/// 0x0262: entity create - what makes a client SEE an entity (the world's most
/// important server message). Header is pinned (opcode + guid); the BODY is
/// heavily BIT-PACKED and variable (type, creatureId, position, faction,
/// display, optional sections), lengths 270..2416 across samples. Position is
/// NOT byte-aligned, so the body needs dedicated bit-level analysis correlated
/// with known worlddb entities - a focused effort, not a byte scan. Marked here
/// so the next pass does not repeat the byte-aligned dead end.
/// </summary>
public sealed record ServerEntityCreate(uint Guid, byte[] Body) : IServerMessage
{
    public GameMessageOpcode Opcode => GameMessageOpcode.ServerEntityCreate;
    public static ServerEntityCreate Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);                       // opcode (pinned)
        uint guid = r.ReadUInt32();           // guid (pinned, validated)
        byte[] body = r.ReadBytes(r.BytesRemaining);   // bit-packed, pending decode
        return new ServerEntityCreate(guid, body);
    }
}

/// <summary>0x0876: an entity relation - source guid + target guid (e.g. X acts on Y).</summary>
public sealed record ServerEntityRelation(uint SourceGuid, uint TargetGuid) : IServerMessage
{
    public GameMessageOpcode Opcode => (GameMessageOpcode)0x0876;
    public static ServerEntityRelation Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);
        return new ServerEntityRelation(r.ReadUInt32(), r.ReadUInt32());
    }
}

/// <summary>0x092F: an entity value update - guid + a u32 value + trailing bytes
/// (value observed constant 1200 in the sample set; likely a stat/vital).</summary>
public sealed record ServerEntityValue(uint Guid, uint Value) : IServerMessage
{
    public GameMessageOpcode Opcode => (GameMessageOpcode)0x092F;
    public static ServerEntityValue Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);
        return new ServerEntityValue(r.ReadUInt32(), r.ReadUInt32());
    }
}

/// <summary>0x07FE: a single u32 (a counter/index; the smallest server message).</summary>
public sealed record ServerCounter(uint Value) : IServerMessage
{
    public GameMessageOpcode Opcode => (GameMessageOpcode)0x07FE;
    public static ServerCounter Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);
        return new ServerCounter(r.ReadUInt32());
    }
}
