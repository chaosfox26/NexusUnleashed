using System;

namespace NexusUnleashed.Network;

public interface IServerMessage
{
    GameMessageOpcode Opcode { get; }
}

public sealed record ServerEntitySmallUpdate(uint Guid, byte Flag) : IServerMessage
{
    public GameMessageOpcode Opcode => GameMessageOpcode.ServerEntitySmallUpdate;
    public static ServerEntitySmallUpdate Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);        return new ServerEntitySmallUpdate(r.ReadUInt32(), r.ReadByte());
    }
}

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

public sealed record ServerEntityCreate(uint Guid, float X, float Y, float Z) : IServerMessage
{
    public GameMessageOpcode Opcode => GameMessageOpcode.ServerEntityCreate;

    private const int PositionBit = 289;

    public static ServerEntityCreate Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);        uint guid = r.ReadUInt32();        int skip = PositionBit - 48;        while (skip > 0) { int c = System.Math.Min(32, skip); r.ReadBits(c); skip -= c; }
        return new ServerEntityCreate(guid, r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}

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

public sealed record ServerWorldInit(uint[] Ids) : IServerMessage
{
    public GameMessageOpcode Opcode => (GameMessageOpcode)0x0981;

    public static ServerWorldInit Parse(byte[] payload)
    {
        var r = new PacketReader(payload);
        r.ReadBits(16);        uint count = r.ReadUInt32();
        var ids = new uint[count];
        for (uint i = 0; i < count; i++) ids[i] = r.ReadUInt32();
        return new ServerWorldInit(ids);
    }

    public byte[] Build()
    {
        var w = new PacketWriter();
        w.WriteBits(0x0981, 16);
        w.WriteBits((uint)Ids.Length, 32);
        foreach (uint id in Ids) w.WriteBits(id, 32);
        return w.ToArray();
    }
}

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
