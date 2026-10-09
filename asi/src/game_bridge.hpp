// The connection between CS:GTA and the running game: finding GTA V's native functions and running
// our code once per frame on the game's script thread. Everything build-specific lives behind this
// interface; game_bridge_enhanced.cpp implements it for GTA V Enhanced.
#pragma once
#include <cstdint>
#include <cstring>
#include <string>
#include <type_traits>

namespace csgta::game {

// The game's native call context (argument and return slots of 8 bytes each).
struct NativeContext {
    void* ret = nullptr;
    uint32_t argCount = 0;
    void* args = nullptr;
    uint32_t dataCount = 0;
    alignas(16) uint8_t vectorSpace[192] = {};
    alignas(16) uint64_t retBuf[4] = {};
    alignas(16) uint64_t argBuf[32] = {};
};
using NativeHandler = void (*)(NativeContext*);

// Locate the native table in this game build. On failure, whyNot says what is missing.
bool Connect(std::string& whyNot);

// The handler for a native, by its documented (nativedb) hash; nullptr if this build has none.
NativeHandler Find(uint64_t hash);

// Run callback on the game's script thread once per frame, from now on.
void RunEachFrame(void (*callback)());

// Game build the bridge was made and checked against, for the log.
const char* TargetBuild();

// ---------------------------------------------------------------- calling natives

struct Vec3 { float x; uint32_t _p0; float y; uint32_t _p1; float z; uint32_t _p2; };

class Call {
public:
    explicit Call(uint64_t hash) : hash_(hash) { ctx_.ret = ctx_.retBuf; ctx_.args = ctx_.argBuf; }

    template <typename T> Call& Arg(T v) {
        uint64_t slot = 0;
        if constexpr (std::is_same_v<T, float>) std::memcpy(&slot, &v, sizeof v);
        else if constexpr (std::is_same_v<T, bool>) slot = v ? 1 : 0;
        else if constexpr (std::is_pointer_v<T>) slot = reinterpret_cast<uint64_t>(v);
        else slot = static_cast<uint64_t>(v);
        if (ctx_.argCount < 32) ctx_.argBuf[ctx_.argCount++] = slot;
        return *this;
    }

    template <typename R = void> R Run() {
        NativeHandler h = Find(hash_);
        if (h) h(&ctx_);
        if constexpr (std::is_void_v<R>) return;
        else {
            R r{};
            if (h) std::memcpy(&r, ctx_.retBuf, sizeof(R));
            return r;
        }
    }

private:
    uint64_t hash_;
    NativeContext ctx_;
};

template <typename R = void, typename... A> R N(uint64_t hash, A... args) {
    Call c(hash);
    (c.Arg(args), ...);
    return c.Run<R>();
}

} // namespace csgta::game
