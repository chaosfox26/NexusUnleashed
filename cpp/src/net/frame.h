// NexusUnleashed - clean-room authored. C++ port of GamePacketFrame.cs. On the wire a
// message is: [u32 LE size (self-inclusive)][u16 LE opcode][bit-packed payload].
// PINNED against the behavioral oracle (spec/protocol/frame.md). Header-only.
#pragma once
#include <cstdint>
#include <vector>
#include <utility>
#include "net/bitstream.h"

namespace nexus::net {

class GamePacketFrame {
public:
    static constexpr int SizeFieldBits   = 32;  // size counts the whole frame incl. itself
    static constexpr int OpcodeFieldBits = 16;

    static std::vector<uint8_t> Encode(uint16_t opcode, const std::vector<uint8_t>& payload) {
        PacketWriter w;
        uint32_t size = static_cast<uint32_t>((SizeFieldBits / 8) + (OpcodeFieldBits / 8) + payload.size());
        w.WriteBits(size, SizeFieldBits);
        w.WriteBits(opcode, OpcodeFieldBits);
        w.WriteBytes(payload.data(), payload.size());
        return w.ToArray();
    }

    /// Peek the self-inclusive frame length; returns false if the buffer is short.
    static bool TryReadLength(const uint8_t* buf, size_t len, size_t& total_bytes) {
        total_bytes = 0;
        if (len < static_cast<size_t>(SizeFieldBits / 8)) return false;
        PacketReader r(buf, len);
        total_bytes = static_cast<size_t>(r.ReadBits(SizeFieldBits));
        return len >= total_bytes;
    }

    static std::pair<uint16_t, std::vector<uint8_t>> Decode(const std::vector<uint8_t>& frame) {
        PacketReader r(frame);
        r.ReadBits(SizeFieldBits);
        uint16_t opcode = static_cast<uint16_t>(r.ReadBits(OpcodeFieldBits));
        std::vector<uint8_t> payload = r.ReadBytes(r.BytesRemaining());
        return { opcode, std::move(payload) };
    }
};

} // namespace nexus::net
