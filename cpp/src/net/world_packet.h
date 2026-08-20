// NexusUnleashed - clean-room authored. C++ port of WorldPacket.cs — the world channel's
// encrypted packed container:
//   outer frame : [u32 size][u16 containerOpcode][container payload]
//   container   : [u32 innerLen self-inclusive][encrypted [u16 op][bit-body]]
// containerOpcode = 0x03DC S->C, 0x0244 C->S. NOTE: the realm channel (23115) sends
// S->C as a CLEAR frame, not this container — see spec/protocol/char-list-0x117.md.
// This codec is for the world channel / the inbound 0x0244 decode.
#pragma once
#include <cstdint>
#include <vector>
#include <utility>
#include "crypto/packet_crypt.h"

namespace nexus::net {

class WorldPacket {
public:
    // S->C container opcode. VERIFIED LIVE: the router (sub_140014F10) has a NON-NULL decrypt
    // handler for 0x76 (a1[708]) but a NULL one for 0x3DC (a1[709]) at account-retrieval time.
    // So the realm/auth S->C container is 0x0076 (0x03DC is world-channel only, decoder not yet
    // installed). Decrypt runs `(*(handler[0x10]+0x20))(...)` only if the handler is non-null.
    static constexpr uint16_t ServerContainer = 0x0076;
    static constexpr uint16_t ClientContainer = 0x0244;
    static constexpr uint64_t WorldChannelSeed = crypto::PacketCrypt::AuthChannelKey;
    static constexpr uint64_t RealmLaneKey     = crypto::PacketCrypt::RealmLaneKey;

    // crypt is mutated (continuous stream), so it is a non-const reference.
    static std::vector<uint8_t> EncodeServer(uint16_t innerOpcode, const std::vector<uint8_t>& innerBody,
                                             crypto::PacketCrypt& crypt);
    // Same, but with an explicit container opcode (0x76 = connection, 0x03DC = account/world).
    static std::vector<uint8_t> EncodeServerVia(uint16_t containerOpcode, uint16_t innerOpcode,
                                                const std::vector<uint8_t>& innerBody, crypto::PacketCrypt& crypt);
    static std::vector<uint8_t> EncodeClient(uint16_t innerOpcode, const std::vector<uint8_t>& innerBody,
                                             crypto::PacketCrypt& crypt);
    static std::pair<uint16_t, std::vector<uint8_t>> DecodeContainer(const std::vector<uint8_t>& containerPayload,
                                                                     crypto::PacketCrypt& crypt);
};

} // namespace nexus::net
