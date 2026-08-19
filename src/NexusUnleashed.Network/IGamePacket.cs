// NexusUnleashed - clean-room authored. The message contract: every game
// message reads/writes itself against the bit buffer. Opcode identity is a
// protocol fact (from the client / our datamine), carried per message type.
namespace NexusUnleashed.Network;

/// <summary>A message that can serialize itself to/from the bit-packed wire.</summary>
public interface IGamePacket
{
    /// <summary>The client opcode for this message (a protocol fact).</summary>
    ushort Opcode { get; }

    void Read(PacketReader reader);
    void Write(PacketWriter writer);
}
