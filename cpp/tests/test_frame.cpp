#include "test.h"
#include "net/frame.h"
#include "net/world_packet.h"
#include "proto/character_list.h"
#include "proto/account_realm.h"
#include <vector>
using namespace nexus;

int run_frame_tests() {
    int fails = 0;

    // Frame: [u32 size self-inclusive][u16 op][payload]; round-trips.
    { std::vector<uint8_t> body = {0xAA, 0xBB, 0xCC};
      auto frame = net::GamePacketFrame::Encode(0x0117, body);
      CHECK(frame.size() == 4 + 2 + 3);
      CHECK(frame[0] == 9);              // size LE = 9
      CHECK(frame[4] == 0x17 && frame[5] == 0x01);  // opcode LE
      auto [op, payload] = net::GamePacketFrame::Decode(frame);
      CHECK(op == 0x0117);
      CHECK(payload == body);
      size_t total = 0;
      CHECK(net::GamePacketFrame::TryReadLength(frame.data(), frame.size(), total));
      CHECK(total == frame.size()); }

    // Container: encode inner (op, body) -> outer frame -> decode -> recover. The cipher is a
    // continuous stream, so the two endpoints use SEPARATE crypt instances (both fresh from the
    // same seed); the first message on each is at the initial register, so it round-trips.
    { crypto::PacketCrypt encCrypt(net::WorldPacket::WorldChannelSeed);
      crypto::PacketCrypt decCrypt(net::WorldPacket::WorldChannelSeed);
      std::vector<uint8_t> inner = {1,2,3,4,5,6,7,8,9,10};
      auto frame = net::WorldPacket::EncodeClient(0x0592, inner, encCrypt);
      auto [outerOp, containerPayload] = net::GamePacketFrame::Decode(frame);
      CHECK(outerOp == net::WorldPacket::ClientContainer);
      auto [innerOp, innerBody] = net::WorldPacket::DecodeContainer(containerPayload, decCrypt);
      CHECK(innerOp == 0x0592);
      CHECK(innerBody == inner); }

    // Char list: 1 char (3-char name) -> 121 bytes (964 bits), header u64=0, count=1.
    { std::vector<proto::CharacterRecord> chars(1);
      chars[0].Id = 22; chars[0].Name = "Abc";
      chars[0].Sex = 0; chars[0].Race = 4; chars[0].Class = 3; chars[0].Level = 1;
      auto body = proto::CharacterListMessage::Build(chars);
      CHECK(body.size() == 121);
      // header u64 = 0
      for (int i = 0; i < 8; ++i) CHECK(body[i] == 0);
      // count u32 = 1 (LE)
      CHECK(body[8] == 1 && body[9] == 0 && body[10] == 0 && body[11] == 0); }

    // Empty char list -> 50 bytes (395 bits).
    { auto body = proto::CharacterListMessage::Build({});
      CHECK(body.size() == 50); }

    // 0x7A1 account data: 32 + {32+16+16+64} + {32+16} + 1 + wstr(1+7) + 32 + 2 + 21
    //   = 272 bits = 34 bytes.
    { auto body = proto::AccountRealmMessages::BuildAccountData();
      CHECK(body.size() == 34); }

    // 0x761 empty realm list: u64 header + u32 count1(0) + u32 count2(0) = 16 bytes.
    { auto body = proto::AccountRealmMessages::BuildRealmList({});
      CHECK(body.size() == 16);
      for (int i = 0; i < 8; ++i) CHECK(body[i] == 0);            // header
      for (int i = 8; i < 16; ++i) CHECK(body[i] == 0); }        // both counts = 0

    // 0x761 with one realm: count1 = 1 (LE) right after the u64 header.
    { std::vector<proto::RealmEntry> realms(1);
      realms[0].Id = 1; realms[0].Name = "NexusUnleashed"; realms[0].Host = "127.0.0.1";
      auto body = proto::AccountRealmMessages::BuildRealmList(realms);
      CHECK(body.size() > 16);
      CHECK(body[8] == 1 && body[9] == 0 && body[10] == 0 && body[11] == 0); }

    std::printf("[frame/container/charlist] %s\n", fails ? "FAILED" : "ok");
    return fails;
}
