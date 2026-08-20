#include <cstdio>

int run_bitstream_tests();
int run_packet_crypt_tests();
int run_frame_tests();
int run_crypto2_tests();
int run_sts_tests();

int main() {
    std::printf("=== NexusUnleashed C++ port — Phase 1/2 byte-verification ===\n");
    int fails = 0;
    fails += run_bitstream_tests();
    fails += run_packet_crypt_tests();
    fails += run_frame_tests();
    fails += run_crypto2_tests();
    fails += run_sts_tests();
    std::printf("=== %s (%d failure%s) ===\n", fails ? "TESTS FAILED" : "ALL TESTS PASSED",
                fails, fails == 1 ? "" : "s");
    return fails ? 1 : 0;
}
