// Minimal dependency-free test harness for the Phase-1 byte-verification.
#pragma once
#include <cstdio>
#define CHECK(cond) do { if(!(cond)){ std::printf("  FAIL %s:%d  %s\n", __FILE__, __LINE__, #cond); ++fails; } } while(0)
