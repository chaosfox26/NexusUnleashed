#include "test.h"
#include "crypto/arc4.h"
#include "crypto/sts_srp.h"
#include <vector>
#include <string>
using namespace nexus::crypto;

int run_crypto2_tests() {
    int fails = 0;

    // RC4 canonical known-answer vector: RC4("Key","Plaintext") = BBF316E8D940AF0AD3.
    { std::vector<uint8_t> key = {'K','e','y'};
      std::vector<uint8_t> data = {'P','l','a','i','n','t','e','x','t'};
      Arc4 c; c.PrepareKey(key); c.ProcessBuffer(data);
      const uint8_t expect[9] = {0xBB,0xF3,0x16,0xE8,0xD9,0x40,0xAF,0x0A,0xD3};
      for (int i = 0; i < 9; ++i) CHECK(data[i] == expect[i]); }

    // RC4 is symmetric (two fresh keyed instances round-trip).
    { std::vector<uint8_t> key(16, 0x5A);
      std::vector<uint8_t> msg; for (int i = 0; i < 200; ++i) msg.push_back((uint8_t)(i*3+1));
      std::vector<uint8_t> orig = msg;
      Arc4 enc; enc.PrepareKey(key); enc.ProcessBuffer(msg);
      Arc4 dec; dec.PrepareKey(key); dec.ProcessBuffer(msg);
      CHECK(msg == orig); }

    // SRP: server StartHandshake yields a 128-byte, non-zero B.
    { std::vector<uint8_t> salt(16, 0x11);
      std::vector<uint8_t> verifier(128); for (int i = 0; i < 128; ++i) verifier[i] = (uint8_t)(i+1);
      StsSrp srp(salt, verifier, "user@example.com");
      auto B = srp.StartHandshake();
      CHECK(B.size() == 128);
      bool nonzero = false; for (auto x : B) if (x) { nonzero = true; break; }
      CHECK(nonzero); }

    std::printf("[arc4/srp] %s\n", fails ? "FAILED" : "ok");
    return fails;
}
