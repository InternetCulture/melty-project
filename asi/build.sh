#!/usr/bin/env bash
# Builds CSGTA.asi (Windows x64) with MinGW-w64, or runs the logic tests with "./build.sh test".
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p ../dist/asi
if [ "${1:-}" = "test" ]; then
  g++ -std=c++17 -O1 -Wall src/cs_logic.cpp tests/logic_tests.cpp -o ../dist/asi/logic_tests
  ../dist/asi/logic_tests
  exit
fi
x86_64-w64-mingw32-g++-posix -std=c++17 -O2 -Wall -shared -static -s \
  -DUNICODE -D_UNICODE -DWIN32_LEAN_AND_MEAN -DNOMINMAX \
  src/main.cpp src/mod.cpp src/audio.cpp src/cs_logic.cpp src/game_bridge_enhanced.cpp \
  -o ../dist/asi/CSGTA.asi -lwinmm -lshell32 -lole32 -luuid
echo dist/asi/CSGTA.asi
