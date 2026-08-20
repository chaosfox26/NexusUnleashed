namespace NexusUnleashed.Network;

public interface IGamePacket
{
    ushort Opcode { get; }

    void Read(PacketReader reader);
    void Write(PacketWriter writer);
}
