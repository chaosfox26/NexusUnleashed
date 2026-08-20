#include "crypto/sts_srp.h"
#include <openssl/bn.h>
#include <openssl/sha.h>
#include <openssl/rand.h>
#include <algorithm>
#include <stdexcept>

namespace nexus::crypto {

static const uint8_t NB[128] = {
    0xE3,0x06,0xEB,0xC0,0x2F,0x1D,0xC6,0x9F,0x5B,0x43,0x76,0x83,0xFE,0x38,0x51,0xFD,
    0x9A,0xAA,0x6E,0x97,0xF4,0xCB,0xD4,0x2F,0xC0,0x6C,0x72,0x05,0x3C,0xBC,0xED,0x68,
    0xEC,0x57,0x0E,0x66,0x66,0xF5,0x29,0xC5,0x85,0x18,0xCF,0x7B,0x29,0x9B,0x55,0x82,
    0x49,0x5D,0xB1,0x69,0xAD,0xF4,0x8E,0xCE,0xB6,0xD6,0x54,0x61,0xB4,0xD7,0xC7,0x5D,
    0xD1,0xDA,0x89,0x60,0x1D,0x5C,0x49,0x8E,0xE4,0x8B,0xB9,0x50,0xE2,0xD8,0xD5,0xE0,
    0xE0,0xC6,0x92,0xD6,0x13,0x48,0x3B,0x38,0xD3,0x81,0xEA,0x96,0x74,0xDF,0x74,0xD6,
    0x76,0x65,0x25,0x9C,0x4C,0x31,0xA2,0x9E,0x0B,0x3C,0xFF,0x75,0x87,0x61,0x72,0x60,
    0xE8,0xC5,0x8F,0xFA,0x0A,0xF8,0x33,0x9C,0xD6,0x8D,0xB3,0xAD,0xB9,0x0A,0xAF,0xEE
};
static const uint8_t gBytes[4] = { 2, 0, 0, 0 };

using Bytes = std::vector<uint8_t>;

static Bytes sha256(const Bytes& d) {
    Bytes out(SHA256_DIGEST_LENGTH);
    SHA256(d.data(), d.size(), out.data());
    return out;
}
static Bytes combine(std::initializer_list<Bytes> parts) {
    Bytes r; size_t n = 0; for (auto& p : parts) n += p.size(); r.reserve(n);
    for (auto& p : parts) r.insert(r.end(), p.begin(), p.end());
    return r;
}
static Bytes reverse_uint32(const Bytes& d) {
    Bytes r(d.size());
    for (size_t i = 0; i < d.size(); i += 4)
        std::copy(d.begin() + i, d.begin() + i + 4, r.begin() + (r.size() - (i + 4)));
    return r;
}
static BIGNUM* le_to_bn(const uint8_t* le, size_t len) { return BN_lebin2bn(le, static_cast<int>(len), nullptr); }
static BIGNUM* le_to_bn(const Bytes& le) { return le_to_bn(le.data(), le.size()); }
static Bytes bn_to_le(const BIGNUM* v, int count) {
    Bytes out(static_cast<size_t>(count));
    BN_bn2lebinpad(v, out.data(), count);
    return out;
}

struct StsSrp::Impl {
    BN_CTX* ctx = BN_CTX_new();
    BIGNUM* N = nullptr; BIGNUM* g = nullptr; BIGNUM* k = nullptr;
    BIGNUM* v = nullptr; BIGNUM* b = nullptr;
    Bytes salt, I;
    ~Impl() {
        BN_free(N); BN_free(g); BN_free(k); BN_free(v); BN_free(b); BN_CTX_free(ctx);
    }
};

StsSrp::StsSrp(const Bytes& salt, const Bytes& verifier, const std::string& username)
    : p_(std::make_unique<Impl>()) {
    p_->N = le_to_bn(NB, 128);
    p_->g = BN_new(); BN_set_word(p_->g, 2);
    Bytes kh = reverse_uint32(sha256(combine({ Bytes(NB, NB + 128), Bytes(gBytes, gBytes + 4) })));
    p_->k = le_to_bn(kh);
    p_->salt = salt;
    p_->v = le_to_bn(verifier);
    p_->I = sha256(Bytes(username.begin(), username.end()));
}

StsSrp::~StsSrp() = default;

std::vector<uint8_t> StsSrp::StartHandshake() {
    uint8_t rnd[0x20];
    if (RAND_bytes(rnd, sizeof(rnd)) != 1) throw std::runtime_error("RAND_bytes failed");
    p_->b = le_to_bn(rnd, sizeof(rnd));

    BN_CTX* ctx = p_->ctx;
    BIGNUM* gb = BN_new(); BN_mod_exp(gb, p_->g, p_->b, p_->N, ctx);
    BIGNUM* kv = BN_new(); BN_mul(kv, p_->k, p_->v, ctx);
    BIGNUM* sum = BN_new(); BN_add(sum, kv, gb);
    BIGNUM* B = BN_new(); BN_mod(B, sum, p_->N, ctx);

    b_wire_ = bn_to_le(B, 0x80);
    BN_free(gb); BN_free(kv); BN_free(sum); BN_free(B);
    return b_wire_;
}

static Bytes interleave_session_key(const Bytes& s) {
    auto it0 = std::find(s.begin(), s.end(), uint8_t(0));
    int first0 = (it0 == s.end()) ? -1 : static_cast<int>(it0 - s.begin());
    int start = static_cast<int>(s.size()) - 1;
    int length = 4;
    if (first0 != -1 && first0 < static_cast<int>(s.size()) - 4) length = static_cast<int>(s.size()) - first0;

    Bytes p1(length >> 1), p2(length >> 1);
    for (int i = 0, j = start, kk = start - 1; i < static_cast<int>(p1.size()); ++i, j -= 2, kk -= 2) {
        p1[i] = s[j]; p2[i] = s[kk];
    }
    p1 = sha256(p1); p2 = sha256(p2);

    Bytes key(s.size() / 2, 0);
    for (int i = 0; i < static_cast<int>(p1.size()) && i * 2 + 1 < static_cast<int>(key.size()); ++i) {
        key[i * 2] = p1[i]; key[i * 2 + 1] = p2[i];
    }
    return key;
}

bool StsSrp::Verify(const Bytes& aLe, const Bytes& m1, Bytes& m2, Bytes& sessionKey) {
    m2.clear(); sessionKey.clear();
    BN_CTX* ctx = p_->ctx;

    BIGNUM* A = le_to_bn(aLe);
    BIGNUM* Amod = BN_new(); BN_mod(Amod, A, p_->N, ctx);
    if (BN_is_zero(A) || BN_is_zero(Amod)) { BN_free(A); BN_free(Amod); return false; }
    BN_free(Amod);

    Bytes uh = reverse_uint32(sha256(combine({ aLe, b_wire_ })));
    BIGNUM* u = le_to_bn(uh);

    BIGNUM* vu = BN_new(); BN_mod_exp(vu, p_->v, u, p_->N, ctx);
    BIGNUM* base = BN_new(); BN_mod_mul(base, A, vu, p_->N, ctx);
    BIGNUM* S = BN_new(); BN_mod_exp(S, base, p_->b, p_->N, ctx);

    Bytes K = interleave_session_key(bn_to_le(S, 0x80));

    Bytes nH = sha256(Bytes(NB, NB + 128));
    Bytes gH = sha256(Bytes(gBytes, gBytes + 4));
    for (size_t i = 0; i < nH.size(); ++i) nH[i] ^= gH[i];
    Bytes expected = sha256(combine({ nH, p_->I, p_->salt, aLe, b_wire_, K }));

    bool ok = (expected.size() == m1.size());
    if (ok) { int d = 0; for (size_t i = 0; i < expected.size(); ++i) d |= expected[i] ^ m1[i]; ok = (d == 0); }

    if (ok) {
        m2 = sha256(combine({ aLe, m1, K }));
        sessionKey = K;
    }
    BN_free(A); BN_free(u); BN_free(vu); BN_free(base); BN_free(S);
    return ok;
}

} // namespace nexus::crypto
