#pragma once
#include <cstdint>
#include <vector>
#include <utility>
#include "net/bitstream.h"

namespace nexus::net {

class GamePacketFrame {
public:
    static constexpr int SizeFieldBits   = 32;
    static constexpr int OpcodeFieldBits = 16;

    static std::vector<uint8_t> Encode(uint16_t opcode, const std::vector<uint8_t>& payload) {
        PacketWriter w;
        uint32_t size = static_cast<uint32_t>((SizeFieldBits / 8) + (OpcodeFieldBits / 8) + payload.size());
        w.WriteBits(size, SizeFieldBits);
        w.WriteBits(opcode, OpcodeFieldBits);
        w.WriteBytes(payload.data(), payload.size());
        return w.ToArray();
    }

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
