// NexusUnleashed - clean-room authored. See world_packet.h. 1:1 with WorldPacket.cs.
#include "net/world_packet.h"
#include "net/frame.h"
#include <stdexcept>
#include <cstring>

namespace nexus::net {

static uint32_t ReadU32LE(const uint8_t* b) {
    return static_cast<uint32_t>(b[0]) | (static_cast<uint32_t>(b[1]) << 8)
         | (static_cast<uint32_t>(b[2]) << 16) | (static_cast<uint32_t>(b[3]) << 24);
}
static void WriteU32LE(uint8_t* b, uint32_t v) {
    b[0] = static_cast<uint8_t>(v); b[1] = static_cast<uint8_t>(v >> 8);
    b[2] = static_cast<uint8_t>(v >> 16); b[3] = static_cast<uint8_t>(v >> 24);
}

// container payload = [u32 innerLen self-inclusive][encrypted [u16 op][body]].
// serverToClient selects the cipher: S->C uses the QWORD cipher (EncryptForClient); C->S uses
// the BYTE cipher (Encrypt) — the two directions differ (see packet_crypt.h).
static std::vector<uint8_t> BuildContainerPayload(uint16_t innerOpcode,
                                                  const std::vector<uint8_t>& innerBody,
                                                  crypto::PacketCrypt& crypt, bool serverToClient) {
    std::vector<uint8_t> inner(2 + innerBody.size());
    inner[0] = static_cast<uint8_t>(innerOpcode);
    inner[1] = static_cast<uint8_t>(innerOpcode >> 8);
    std::memcpy(inner.data() + 2, innerBody.data(), innerBody.size());

    std::vector<uint8_t> cipher = serverToClient ? crypt.EncryptForClient(inner) : crypt.Encrypt(inner);

    std::vector<uint8_t> payload(4 + cipher.size());
    WriteU32LE(payload.data(), static_cast<uint32_t>(4 + cipher.size()));
    std::memcpy(payload.data() + 4, cipher.data(), cipher.size());
    return payload;
}

std::vector<uint8_t> WorldPacket::EncodeServer(uint16_t innerOpcode, const std::vector<uint8_t>& innerBody,
                                               crypto::PacketCrypt& crypt) {
    return GamePacketFrame::Encode(ServerContainer, BuildContainerPayload(innerOpcode, innerBody, crypt, true));
}

std::vector<uint8_t> WorldPacket::EncodeServerVia(uint16_t containerOpcode, uint16_t innerOpcode,
                                                  const std::vector<uint8_t>& innerBody, crypto::PacketCrypt& crypt) {
    return GamePacketFrame::Encode(containerOpcode, BuildContainerPayload(innerOpcode, innerBody, crypt, true));
}

std::vector<uint8_t> WorldPacket::EncodeClient(uint16_t innerOpcode, const std::vector<uint8_t>& innerBody,
                                               crypto::PacketCrypt& crypt) {
    return GamePacketFrame::Encode(ClientContainer, BuildContainerPayload(innerOpcode, innerBody, crypt, false));
}

std::pair<uint16_t, std::vector<uint8_t>> WorldPacket::DecodeContainer(const std::vector<uint8_t>& containerPayload,
                                                                       crypto::PacketCrypt& crypt) {
    if (containerPayload.size() < 4)
        throw std::invalid_argument("container payload shorter than its length field");
    uint32_t innerLen = ReadU32LE(containerPayload.data());
    if (innerLen < 4 || innerLen > containerPayload.size())
        throw std::invalid_argument("container innerLen out of range");

    size_t cipherLen = innerLen - 4;
    std::vector<uint8_t> cipher(containerPayload.begin() + 4, containerPayload.begin() + 4 + cipherLen);
    std::vector<uint8_t> inner = crypt.Decrypt(cipher);
    if (inner.size() < 2)
        throw std::invalid_argument("decrypted inner shorter than an opcode");

    uint16_t opcode = static_cast<uint16_t>(inner[0] | (inner[1] << 8));
    std::vector<uint8_t> body(inner.begin() + 2, inner.end());
    return { opcode, std::move(body) };
}

} // namespace nexus::net
