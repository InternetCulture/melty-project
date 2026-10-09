// GTA V Enhanced implementation of the game bridge.
//
// NOT FINISHED. Finding Enhanced's native table, mapping documented native hashes to this build's
// handlers, and hooking a per-frame point on the script thread all depend on the exact game exe,
// so they have to be worked out and checked with GTA V Enhanced running. Until then Connect()
// returns false and CS:GTA stays inactive (the game plays as normal, the log says why).
//
// Rules for finishing it:
//  - Never touch, patch, or hide from BattlEye; read only the game's own code and data.
//  - Run natives only on the game's script thread (RunEachFrame), never from our own threads.
//  - Do nothing when GTA Online is active (mod.cpp also checks NETWORK_IS_SESSION_STARTED).
#include "game_bridge.hpp"

namespace csgta::game {

namespace {
void (*g_frameCallback)() = nullptr;
}

bool Connect(std::string& whyNot) {
    whyNot = "the GTA V Enhanced game connection is not built yet";
    return false;
}

NativeHandler Find(uint64_t) { return nullptr; }

void RunEachFrame(void (*callback)()) { g_frameCallback = callback; }

const char* TargetBuild() { return "GTA V Enhanced (build not yet pinned)"; }

} // namespace csgta::game
