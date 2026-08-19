// NexusUnleashed - clean-room authored. Proves the bit packer round-trips every
// width and type: what the writer emits, the reader must return identically.
// This is the parity discipline applied to the smallest unit of the protocol.
using System;
using NexusUnleashed.Network;

static class RoundTrip
{
    static int _fails;

    static void Check(string name, bool ok)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}");
        if (!ok) _fails++;
    }

    static int Main()
    {
        Console.WriteLine("PacketReader/PacketWriter round-trip:");

        // mixed-width bit fields
        var w = new PacketWriter();
        w.WriteBits(0b101, 3);
        w.WriteBits(0xDEAD, 16);
        w.WriteBit(true);
        w.WriteBits(0x7FFFFFFFFFFFFFFF, 63);
        var r = new PacketReader(w.ToArray());
        Check("3-bit field", r.ReadBits(3) == 0b101);
        Check("16-bit field", r.ReadBits(16) == 0xDEAD);
        Check("single bit", r.ReadBit());
        Check("63-bit field", r.ReadBits(63) == 0x7FFFFFFFFFFFFFFF);

        // typed primitives
        var w2 = new PacketWriter();
        w2.WriteUInt32(0xCAFEBABE);
        w2.WriteInt16(-1234);
        w2.WriteSingle(3.14159f);
        w2.WriteUInt64(0x0123456789ABCDEF);
        w2.WriteBool(false);
        w2.WriteWideString("Nexus");
        var r2 = new PacketReader(w2.ToArray());
        Check("uint32", r2.ReadUInt32() == 0xCAFEBABE);
        Check("int16 signed", r2.ReadInt16() == -1234);
        Check("float32", Math.Abs(r2.ReadSingle() - 3.14159f) < 1e-6);
        Check("uint64", r2.ReadUInt64() == 0x0123456789ABCDEF);
        Check("bool false", r2.ReadBool() == false);
        Check("widestring", r2.ReadWideString(5) == "Nexus");

        // byte-alignment behavior
        var w3 = new PacketWriter();
        w3.WriteBits(0b11, 2);
        w3.WriteBytes(new byte[] { 0xAA, 0xBB });
        var r3 = new PacketReader(w3.ToArray());
        Check("pre-align 2 bits", r3.ReadBits(2) == 0b11);
        var bytes = r3.ReadBytes(2);
        Check("aligned bytes", bytes[0] == 0xAA && bytes[1] == 0xBB);

        Console.WriteLine(_fails == 0
            ? "\n== ALL ROUND-TRIP TESTS PASS =="
            : $"\n== {_fails} FAILURE(S) ==");
        Console.WriteLine("-- router --");
        int routerFails = Routing.RunAsync().GetAwaiter().GetResult();
        return (_fails == 0 && routerFails == 0) ? 0 : 1;
    }
}
