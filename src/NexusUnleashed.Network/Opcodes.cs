using System.Collections.Generic;

namespace NexusUnleashed.Network;

public enum Bound { ServerToClient, ClientToServer, Unknown }

public sealed record OpcodeInfo(ushort Opcode, string Name, Bound Bound, string Evidence);

public static class Opcodes
{
    public static readonly OpcodeInfo AuthHello =
        new(0x0003, "AuthHello", Bound.ServerToClient, "auth :23115 first S->C frame, 2026-08-19");
    public static readonly OpcodeInfo WorldHello =
        new(0x03DC, "WorldHello", Bound.ServerToClient, "world :24000 first S->C frame (988), 2026-08-19");

    private static readonly Dictionary<ushort, OpcodeInfo> _byId = new();

    static Opcodes()
    {
        Register(AuthHello);
        Register(WorldHello);
    }

    public static void Register(OpcodeInfo info) => _byId[info.Opcode] = info;

    public static bool TryGet(ushort opcode, out OpcodeInfo info) => _byId.TryGetValue(opcode, out info!);

    public static IReadOnlyCollection<OpcodeInfo> Known => _byId.Values;

    public static string NameOf(ushort opcode)
        => _byId.TryGetValue(opcode, out var i) ? i.Name : $"op_0x{opcode:X4}";
}
