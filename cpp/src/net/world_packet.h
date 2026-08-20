#pragma once
#include <cstdint>
#include <vector>
#include <utility>
#include "crypto/packet_crypt.h"

namespace nexus::net {

class WorldPacket {
public:
    static constexpr uint16_t ServerContainer = 0x0076;
    static constexpr uint16_t ClientContainer = 0x0244;
    static constexpr uint64_t WorldChannelSeed = crypto::PacketCrypt::AuthChannelKey;
    static constexpr uint64_t RealmLaneKey     = crypto::PacketCrypt::RealmLaneKey;

    static std::vector<uint8_t> EncodeServer(uint16_t innerOpcode, const std::vector<uint8_t>& innerBody,
                                             crypto::PacketCrypt& crypt);
    static std::vector<uint8_t> EncodeServerVia(uint16_t containerOpcode, uint16_t innerOpcode,
                                                const std::vector<uint8_t>& innerBody, crypto::PacketCrypt& crypt);
    static std::vector<uint8_t> EncodeClient(uint16_t innerOpcode, const std::vector<uint8_t>& innerBody,
                                             crypto::PacketCrypt& crypt);
    static std::pair<uint16_t, std::vector<uint8_t>> DecodeContainer(const std::vector<uint8_t>& containerPayload,
                                                                     crypto::PacketCrypt& crypt);
};

} // namespace nexus::net
