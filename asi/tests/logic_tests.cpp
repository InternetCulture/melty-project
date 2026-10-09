// Unit tests for the game-independent rules. Build and run: asi/build.sh test
#include "../src/cs_logic.hpp"
#include <cstdio>
#include <cstdlib>
#include <set>

using namespace csgta;

static int failures = 0;
#define CHECK(cond) do { if (!(cond)) { std::printf("FAIL %s:%d  %s\n", __FILE__, __LINE__, #cond); failures++; } } while (0)

static const Weapon& W(const char* id) {
    for (auto& w : Weapons()) if (w.id == id) return w;
    std::abort();
}

int main() {
    const float dt = 1.f / 60.f;
    auto ms = [](float u) { return u * kUnitsToMeters; };

    // GTA name hashes
    CHECK(Joaat("WEAPON_PISTOL") == 0x1B06D571u);
    CHECK(Joaat("WEAPON_ASSAULTRIFLE") == 0xBFEFFF6Du);
    CHECK(Joaat("COMPONENT_AT_AR_SUPP_02") == 0xA73D4664u);

    // gun table
    std::set<uint32_t> hashes;
    for (auto& w : Weapons()) {
        hashes.insert(w.gtaHash);
        CHECK((int)w.pattern.size() == w.magSize);
        CHECK(w.pattern[0].pitch == 0.f);
    }
    CHECK(hashes.size() == Weapons().size());
    CHECK(WeaponByGtaHash(Joaat("WEAPON_ASSAULTRIFLE"))->name == "AK-47");
    CHECK(WeaponByGtaHash(Joaat("WEAPON_UNARMED")) == nullptr);

    // movement: top speed, fast start, fast stop
    Movement m;
    V2 v{};
    for (int i = 0; i < 180; i++) v = m.Step(v, {0, 1}, ms(250), true, dt);
    CHECK(v.Length() > ms(250) * 0.97f && v.Length() < ms(250) * 1.001f);
    v = {ms(250), 0};
    int frames = 0;
    while (v.Length() > 0.01f && frames < 600) { v = m.Step(v, {}, 0, true, dt); frames++; }
    CHECK(frames * dt > 0.2f && frames * dt < 0.8f);

    // counter-strafing beats releasing
    {
        float top = ms(215), accurate = top * Movement::kAccurateFraction;
        int rel = 0, ctr = 0;
        V2 a{top, 0};
        while (a.Length() > accurate) { a = m.Step(a, {}, 0, true, dt); rel++; }
        a = {top, 0};
        while (a.x > accurate) { a = m.Step(a, {-1, 0}, top, true, dt); ctr++; }
        CHECK(ctr < rel);
    }

    // bunny hopping: strafe-jumping builds speed past running speed, with no cap
    {
        float run = ms(250);
        V2 b{0, run};
        for (int hop = 0; hop < 10; hop++) {
            for (int i = 0; i < 40; i++) {
                double off = std::acos(std::fmin(1.0, (m.airWishCap * 0.5) / b.Length()));
                double ang = std::atan2(b.y, b.x) + off * (hop % 2 == 0 ? -1 : 1);
                b = m.Step(b, {(float)std::cos(ang), (float)std::sin(ang)}, run, false, dt);
            }
            b = m.Step(b, {}, 0, true, dt, true);
        }
        CHECK(b.Length() > run * 1.3f);
    }

    // heading convention (GTA: 0 = north, counter-clockwise)
    CHECK(Movement::WishDirection(0, 1, 0).y > 0.99f);
    CHECK(Movement::WishDirection(0, 1, 90).x < -0.99f);
    CHECK(Movement::WishDirection(1, 0, 0).x > 0.99f);

    // recoil follows the AK pattern exactly when standing, then settles back
    {
        SprayRecoil r;
        const Weapon& ak = W("ak47");
        float pitch = 0, yaw = 0, t = 0;
        for (int i = 0; i < 29; i++) {
            auto d = r.OnShot(ak, t, 0.f, [] { return 0.5f; });
            pitch += d.pitch; yaw += d.yaw; t += ak.cycleTime;
            CHECK(std::fabs(pitch - ak.pattern[i + 1].pitch) < 1e-3f);
            CHECK(std::fabs(yaw - ak.pattern[i + 1].yaw) < 1e-3f);
        }
        float settled = 0;
        float before = r.PunchPitch();
        for (int i = 0; i < 120; i++) { t += dt; settled += r.Update(&ak, t, dt).pitch; }
        CHECK(std::fabs(before + settled) < 0.01f);
        CHECK(r.ShotIndex() == 0.f);
    }

    // moving shots are thrown off; unscoped AWP too
    CHECK(SprayRecoil::Inaccuracy(&W("ak47"), 1.5f, 5.f, true, false) == 0.f);
    CHECK(SprayRecoil::Inaccuracy(&W("ak47"), 5.f, 5.f, true, false) == 1.f);
    CHECK(SprayRecoil::Inaccuracy(&W("awp"), 0.f, 5.f, true, false) == 1.f);
    CHECK(SprayRecoil::Inaccuracy(&W("awp"), 0.f, 5.f, true, true) == 0.f);

    std::printf(failures ? "%d FAILED\n" : "all logic tests passed\n", failures);
    return failures ? 1 : 0;
}
