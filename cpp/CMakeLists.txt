# NexusUnleashed clean engine — C++ port (1:1 of the C# reference).
# Phase 0: skeleton + pure-leaf components (no external deps yet).
# See Claude/Context/CPP-PORT-PLAN.md for the full plan.
cmake_minimum_required(VERSION 3.20)
project(NexusUnleashed LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(MSVC)
  add_compile_options(/W4 /permissive- /Zc:__cplusplus /utf-8)
endif()

# Dependencies (vcpkg manifest mode).
find_package(OpenSSL REQUIRED)                       # SRP bignum + SHA + RAND
find_package(asio CONFIG REQUIRED)                   # async TCP
find_package(unofficial-libmariadb CONFIG REQUIRED)  # authdb/characterdb
find_package(nlohmann_json CONFIG REQUIRED)          # realm.json

# ---- core library: the pure, dependency-free layer (Phase 1) ----
add_library(nexus_core
  src/net/bitstream.cpp
  src/net/world_packet.cpp
  src/crypto/packet_crypt.cpp
  src/proto/character_list.cpp
  src/proto/character_create.cpp
  src/proto/game_data.cpp
  src/proto/account_realm.cpp
  src/crypto/sts_srp.cpp
  src/crypto/arc4.cpp
  src/sts/sts_message.cpp
  src/sts/sts_server.cpp
  src/sts/auth_flow.cpp
  src/net/game_server.cpp
  src/db/db_store.cpp
  src/realm/world_handshake.cpp
)
target_include_directories(nexus_core PUBLIC src)
target_link_libraries(nexus_core PUBLIC
  OpenSSL::Crypto
  asio::asio
  unofficial::libmariadb
  nlohmann_json::nlohmann_json
)
if(WIN32)
  target_link_libraries(nexus_core PUBLIC ws2_32)
endif()

# ---- realm host executable ----
add_executable(nexus_realm src/realm/main.cpp)
target_link_libraries(nexus_realm PRIVATE nexus_core)

# ---- nusl.exe: the Nexus Unleashed Server Launcher (native Win32 GUI, no deps) ----
if(WIN32)
  add_executable(nusl WIN32 src/launcher/nusl.cpp)
  set_target_properties(nusl PROPERTIES OUTPUT_NAME "nusl")
  target_link_libraries(nusl PRIVATE user32 gdi32 comctl32 psapi)
endif()

# ---- tests (byte-verification against the C# reference vectors) ----
enable_testing()
add_executable(nexus_tests
  tests/test_main.cpp
  tests/test_bitstream.cpp
  tests/test_packet_crypt.cpp
  tests/test_frame.cpp
  tests/test_crypto2.cpp
  tests/test_sts.cpp
)
target_link_libraries(nexus_tests PRIVATE nexus_core)
add_test(NAME nexus_tests COMMAND nexus_tests)
