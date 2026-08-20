#include "test.h"
#include "sts/sts_message.h"
#include <string>
using namespace nexus::sts;

static std::vector<uint8_t> bytes(const std::string& s) { return {s.begin(), s.end()}; }
static std::string str(const std::vector<uint8_t>& v) { return {v.begin(), v.end()}; }

int run_sts_tests() {
    int fails = 0;

    // Parse a complete request.
    { StsParser p;
      p.Feed(bytes("POST /Auth/LoginStart STS/1.0\r\nl:5\r\ns:3\r\n\r\nHELLO"));
      auto req = p.TryReadRequest();
      CHECK(req.has_value());
      if (req) {
        CHECK(req->method == "POST");
        CHECK(req->uri == "/Auth/LoginStart");
        CHECK(req->sequence() == 3);
        CHECK(req->body_text() == "HELLO");
      }
    }

    // Partial framing: head arrives, body doesn't yet -> null; then body -> request.
    { StsParser p;
      p.Feed(bytes("POST /Sts/Connect STS/1.0\r\nl:4\r\ns:0\r\n\r\nAB"));
      CHECK(!p.TryReadRequest().has_value());   // body incomplete (2 of 4)
      p.Feed(bytes("CD"));
      auto req = p.TryReadRequest();
      CHECK(req.has_value());
      if (req) { CHECK(req->uri == "/Sts/Connect"); CHECK(req->body_text() == "ABCD"); }
    }

    // Reply framing: "STS/1.0 200  OK" (two spaces), l:, s:<seq>R.
    { auto frame = StsReply::Ok(4, "<Reply/>");
      std::string s = str(frame);
      CHECK(s == "STS/1.0 200  OK\r\nl:8\r\ns:4R\r\n\r\n<Reply/>"); }

    // Two back-to-back requests in one buffer.
    { StsParser p;
      p.Feed(bytes("POST /A/B STS/1.0\r\nl:1\r\ns:1\r\n\r\nX"
                   "POST /C/D STS/1.0\r\nl:0\r\ns:2\r\n\r\n"));
      auto r1 = p.TryReadRequest(); auto r2 = p.TryReadRequest();
      CHECK(r1 && r1->uri == "/A/B" && r1->sequence() == 1 && r1->body_text() == "X");
      CHECK(r2 && r2->uri == "/C/D" && r2->sequence() == 2 && r2->body.empty()); }

    std::printf("[sts] %s\n", fails ? "FAILED" : "ok");
    return fails;
}
