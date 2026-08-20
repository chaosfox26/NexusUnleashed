#include "test.h"
#include "net/bitstream.h"
#include <vector>
using namespace nexus::net;

int run_bitstream_tests() {
    int fails = 0;

    // LSB-first byte layout: a u16 opcode lands little-endian (matches the frame op).
    { PacketWriter w; w.WriteUInt16(0x0117); auto b = w.ToArray();
      CHECK(b.size() == 2); CHECK(b[0] == 0x17); CHECK(b[1] == 0x01); }

    // Sub-byte packing: 3 bits = 0b101 then 5 zero bits => 0x05 in one byte.
    { PacketWriter w; w.WriteBits(5, 3); w.WriteBits(0, 5); auto b = w.ToArray();
      CHECK(b.size() == 1); CHECK(b[0] == 0x05); }

    // Round-trip across mixed widths (u64, 2b, 5b, 5b, u32, float, wide chars).
    { PacketWriter w;
      w.WriteUInt64(0x1122334455667788ull);
      w.WriteBits(2, 2); w.WriteBits(4, 5); w.WriteBits(3, 5);
      w.WriteUInt32(0xDEADBEEF);
      w.WriteSingle(3.5f);
      w.WriteBit(false); w.WriteBits(3, 7);   // name-style: 1b flag + 7b len
      w.WriteUInt16(u'N'); w.WriteUInt16(u'y'); w.WriteUInt16(u'x');
      auto b = w.ToArray();
      PacketReader r(b);
      CHECK(r.ReadUInt64() == 0x1122334455667788ull);
      CHECK(r.ReadBits(2) == 2u);
      CHECK(r.ReadBits(5) == 4u);
      CHECK(r.ReadBits(5) == 3u);
      CHECK(r.ReadUInt32() == 0xDEADBEEFu);
      CHECK(r.ReadSingle() == 3.5f);
      CHECK(r.ReadBit() == false);
      CHECK(r.ReadBits(7) == 3u);
      CHECK(r.ReadUInt16() == u'N');
      CHECK(r.ReadUInt16() == u'y');
      CHECK(r.ReadUInt16() == u'x');
    }

    std::printf("[bitstream] %s\n", fails ? "FAILED" : "ok");
    return fails;
}
