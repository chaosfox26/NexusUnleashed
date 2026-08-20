#include "test.h"
#include "crypto/packet_crypt.h"
#include <vector>
using namespace nexus::crypto;

int run_packet_crypt_tests() {
    int fails = 0;
    const uint64_t seed = PacketCrypt::AuthChannelKey;
    PacketCrypt c(seed);

    // Round-trip across lengths incl. the qword loop + byte tail, and the 6-byte 0x0591 case.
    for (size_t len : {6u, 8u, 10u, 16u, 123u, 398u}) {
        std::vector<uint8_t> msg;
        for (size_t i = 0; i < len; ++i) msg.push_back(static_cast<uint8_t>(i * 7 + 3));
        CHECK(c.Decrypt(c.Encrypt(msg)) == msg);
        if (len >= 8) CHECK(c.Encrypt(msg) != msg);
    }

    // EncryptForClient is the same routine as Encrypt (one client cipher).
    { std::vector<uint8_t> m; for (int i = 0; i < 50; ++i) m.push_back((uint8_t)(i*5+1));
      CHECK(c.Encrypt(m) == c.EncryptForClient(m)); }

    // KNOWN-ANSWER (captured live from the client): the 0x0592 container's first two qwords.
    // ciphertext -> plaintext, length 398 (so the block-index counter starts at 398*(MULT+1)).
    // Validated offline; here we reproduce it (pad to 16 bytes, but pass the true length 398).
    { std::vector<uint8_t> ct = {
        0xa9,0xc5,0x1c,0x84,0xfc,0x44,0xd7,0x49, 0x9f,0x79,0xea,0x7b,0xb1,0x61,0xc0,0x77 };
      // Reproduce Process with the real length by decrypting a 398-long buffer whose first 16
      // bytes are ct (rest zero) and checking the first 16 output bytes.
      std::vector<uint8_t> buf(398, 0);
      for (size_t i = 0; i < 16; ++i) buf[i] = ct[i];
      auto pt = c.Decrypt(buf);
      const uint8_t expect[16] = {
        0x92,0x05,0xaa,0x3e,0x00,0x00,0x2a,0x41, 0x39,0x1f,0x93,0xdf,0x9d,0x3b,0x10,0x00 };
      for (int i = 0; i < 16; ++i) CHECK(pt[i] == expect[i]); }

    // Determinism.
    { std::vector<uint8_t> m; for (int i = 0; i < 50; ++i) m.push_back((uint8_t)(i*3+2));
      PacketCrypt c2(seed); CHECK(c.Encrypt(m) == c2.Encrypt(m)); }

    std::printf("[packet_crypt] %s\n", fails ? "FAILED" : "ok");
    return fails;
}
