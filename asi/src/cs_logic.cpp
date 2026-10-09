#include "cs_logic.hpp"
#include <algorithm>
#include <cctype>
#include <unordered_map>

namespace csgta {

uint32_t Joaat(const std::string& text) {
    uint32_t h = 0;
    for (unsigned char c : text) {
        h += static_cast<uint32_t>(std::tolower(c));
        h += h << 10;
        h ^= h >> 6;
    }
    h += h << 3;
    h ^= h >> 11;
    h += h << 15;
    return h;
}

namespace {

std::vector<SprayPoint> Points(std::initializer_list<float> yp) {
    std::vector<SprayPoint> r;
    for (auto it = yp.begin(); it != yp.end(); it += 2) r.push_back({*it, *(it + 1)});
    return r;
}

// AK-47: climbs ~9 shots, swings left, then right, then side to side. (yaw right, pitch up), degrees.
const std::vector<SprayPoint> kAk = Points({
    0.00f, 0.00f,  0.00f, 0.60f,  0.05f, 1.30f,  0.10f, 2.20f,  0.05f, 3.20f,
    0.20f, 4.20f,  0.40f, 5.00f,  0.30f, 5.70f,  0.00f, 6.10f, -0.80f, 6.30f,
   -1.60f, 6.40f, -2.20f, 6.50f, -2.00f, 6.70f, -1.40f, 6.80f, -0.40f, 6.80f,
    0.80f, 6.70f,  1.80f, 6.80f,  2.40f, 6.90f,  2.60f, 7.00f,  2.20f, 7.10f,
    1.40f, 7.00f,  0.60f, 7.10f,  0.40f, 7.20f,  1.20f, 7.20f,  2.00f, 7.10f,
    2.60f, 7.20f,  2.40f, 7.30f,  1.60f, 7.20f,  0.60f, 7.30f, -0.20f, 7.30f});

// M4A4: climbs less, drifts right, then left, then wanders.
const std::vector<SprayPoint> kM4 = Points({
    0.00f, 0.00f,  0.00f, 0.50f,  0.05f, 1.10f, -0.05f, 1.90f,  0.05f, 2.80f,
    0.15f, 3.60f,  0.30f, 4.30f,  0.55f, 4.90f,  0.90f, 5.20f,  1.30f, 5.40f,
    1.50f, 5.60f,  1.20f, 5.80f,  0.60f, 5.90f, -0.20f, 6.00f, -1.00f, 6.00f,
   -1.60f, 6.10f, -1.90f, 6.20f, -1.70f, 6.30f, -1.10f, 6.30f, -0.40f, 6.40f,
    0.30f, 6.40f,  0.90f, 6.50f,  1.20f, 6.50f,  0.90f, 6.60f,  0.30f, 6.60f,
   -0.30f, 6.70f, -0.70f, 6.70f, -0.50f, 6.80f, -0.10f, 6.80f,  0.30f, 6.80f});

std::vector<SprayPoint> BuildPattern(const Weapon& w) {
    int n = std::max(1, w.magSize);
    std::vector<SprayPoint> r(n);
    switch (w.cls) {
    case WeaponClass::Rifle: case WeaponClass::Smg: case WeaponClass::MachineGun: {
        const auto& src = (w.id == "m4a4" || w.id == "m4a1_silencer" || w.id == "famas") ? kM4 : kAk;
        float scale = (w.id == "ak47" || w.id == "m4a4") ? 1.f : w.recoilScale;
        SprayPoint last = src.back();
        for (int i = 0; i < n; i++) {
            if (i < (int)src.size()) r[i] = {src[i].yaw * scale, src[i].pitch * scale};
            else {
                float t = float(i - (int)src.size() + 1);
                r[i] = {(last.yaw + 2.2f * std::sin(t * 0.55f)) * scale, (last.pitch + 0.05f * t) * scale};
            }
        }
        break;
    }
    case WeaponClass::Pistol:
        for (int i = 0; i < n; i++)
            r[i] = {0.25f * std::sin(i * 1.7f) * w.recoilScale * 2.f, i * 1.6f * w.recoilScale};
        break;
    default:
        for (int i = 0; i < n; i++) r[i] = {0.f, i * 3.0f * w.recoilScale};
        break;
    }
    return r;
}

std::vector<Weapon> BuildWeapons() {
    using C = WeaponClass;
    std::vector<Weapon> v = {
        // id, name, gta, class, dmg, cycle, mag, reload, speed, scoped, recoil, moveInacc
        {"glock", "Glock-18", "WEAPON_PISTOL", C::Pistol, 30, 0.15f, 20, 2.27f, 240, 0, 0.30f, 1.2f},
        {"usp_silencer", "USP-S", "WEAPON_COMBATPISTOL", C::Pistol, 35, 0.17f, 12, 2.17f, 240, 0, 0.35f, 1.2f},
        {"p250", "P250", "WEAPON_SNSPISTOL", C::Pistol, 38, 0.15f, 13, 2.2f, 240, 0, 0.40f, 1.4f},
        {"fiveseven", "Five-SeveN", "WEAPON_HEAVYPISTOL", C::Pistol, 32, 0.15f, 20, 2.2f, 240, 0, 0.35f, 1.3f},
        {"deagle", "Desert Eagle", "WEAPON_PISTOL50", C::Pistol, 53, 0.225f, 7, 2.2f, 230, 0, 1.10f, 2.6f},
        {"tec9", "Tec-9", "WEAPON_MACHINEPISTOL", C::Pistol, 33, 0.12f, 18, 2.5f, 240, 0, 0.45f, 1.0f},
        {"mac10", "MAC-10", "WEAPON_MICROSMG", C::Smg, 29, 0.075f, 30, 2.6f, 240, 0, 0.55f, 0.8f},
        {"mp5sd", "MP5-SD", "WEAPON_SMG", C::Smg, 27, 0.08f, 30, 3.0f, 235, 0, 0.45f, 0.8f},
        {"p90", "P90", "WEAPON_ASSAULTSMG", C::Smg, 26, 0.07f, 50, 3.3f, 230, 0, 0.45f, 0.9f},
        {"ak47", "AK-47", "WEAPON_ASSAULTRIFLE", C::Rifle, 36, 0.1f, 30, 2.43f, 215, 0, 1.0f, 3.0f},
        {"m4a4", "M4A4", "WEAPON_CARBINERIFLE", C::Rifle, 33, 0.09f, 30, 3.07f, 225, 0, 0.80f, 2.6f},
        {"m4a1_silencer", "M4A1-S", "WEAPON_SPECIALCARBINE", C::Rifle, 38, 0.1f, 20, 3.07f, 225, 0, 0.70f, 2.4f},
        {"sg556", "SG 553", "WEAPON_ADVANCEDRIFLE", C::Rifle, 30, 0.09f, 30, 2.8f, 210, 0, 0.95f, 3.0f},
        {"famas", "FAMAS", "WEAPON_BULLPUPRIFLE", C::Rifle, 26, 0.09f, 25, 3.3f, 220, 0, 0.70f, 2.4f},
        {"galilar", "Galil AR", "WEAPON_COMPACTRIFLE", C::Rifle, 30, 0.09f, 35, 3.0f, 215, 0, 0.80f, 2.6f},
        {"awp", "AWP", "WEAPON_HEAVYSNIPER", C::Sniper, 115, 1.455f, 5, 3.67f, 200, 100, 1.0f, 6.0f},
        {"ssg08", "SSG 08", "WEAPON_SNIPERRIFLE", C::Sniper, 88, 1.25f, 10, 3.7f, 230, 0, 0.6f, 3.5f},
        {"nova", "Nova", "WEAPON_PUMPSHOTGUN", C::Shotgun, 26, 0.88f, 8, 3.5f, 220, 0, 1.0f, 1.5f},
        {"xm1014", "XM1014", "WEAPON_ASSAULTSHOTGUN", C::Shotgun, 20, 0.35f, 7, 3.0f, 215, 0, 0.8f, 1.5f},
        {"negev", "Negev", "WEAPON_MG", C::MachineGun, 35, 0.075f, 150, 5.7f, 150, 0, 0.9f, 3.5f},
        {"m249", "M249", "WEAPON_COMBATMG", C::MachineGun, 32, 0.08f, 100, 5.7f, 195, 0, 0.8f, 3.5f},
    };
    for (auto& w : v) {
        w.gtaHash = Joaat(w.gtaWeapon);
        w.pattern = BuildPattern(w);
    }
    return v;
}

float Decay(float v, float dt) {
    if (v == 0.f) return 0.f;
    float mag = std::fabs(v) * std::exp(-SprayRecoil::kDecayExp * dt) - SprayRecoil::kDecayLinear * dt;
    if (mag <= 0.001f) return 0.f;
    return v > 0 ? mag : -mag;
}

} // namespace

const std::vector<Weapon>& Weapons() {
    static const std::vector<Weapon> all = BuildWeapons();
    return all;
}

const Weapon* WeaponByGtaHash(uint32_t hash) {
    static const std::unordered_map<uint32_t, const Weapon*> map = [] {
        std::unordered_map<uint32_t, const Weapon*> m;
        for (auto& w : Weapons()) m[w.gtaHash] = &w;
        return m;
    }();
    auto it = map.find(hash);
    return it == map.end() ? nullptr : it->second;
}

// ------------------------------------------------------------------ movement

static V2 Accel(V2 v, V2 wishDir, float wishSpeed, float accelBase, float accel, float dt) {
    if (wishSpeed <= 0.f || (wishDir.x == 0.f && wishDir.y == 0.f)) return v;
    float add = wishSpeed - V2::Dot(v, wishDir);
    if (add <= 0.f) return v;
    return v + wishDir * std::fmin(accel * dt * accelBase, add);
}

V2 Movement::Step(V2 v, V2 wishDir, float wishSpeed, bool onGround, float dt, bool skipFriction) const {
    if (dt <= 0.f) return v;
    if (onGround) {
        if (!skipFriction) {
            float speed = v.Length();
            if (speed < 0.01f) v = {};
            else {
                float control = speed < stopSpeed ? stopSpeed : speed;
                float newSpeed = std::fmax(0.f, speed - control * friction * dt);
                v = v * (newSpeed / speed);
            }
        }
        return Accel(v, wishDir, wishSpeed, wishSpeed, accelerate, dt);
    }
    return Accel(v, wishDir, std::fmin(wishSpeed, airWishCap), wishSpeed, airAccelerate, dt);
}

float Movement::MaxSpeedMeters(const Weapon* w, bool armed, bool scoped) {
    float units = !w ? (armed ? kUnmappedGunSpeed : kKnifeSpeed)
                     : (scoped && w->scopedSpeed > 0.f ? w->scopedSpeed : w->maxSpeed);
    return units * kUnitsToMeters;
}

V2 Movement::WishDirection(float right, float forward, float headingDeg) {
    if (V2{right, forward}.Length() < 0.05f) return {};
    double h = headingDeg * 3.14159265358979 / 180.0;
    V2 fwd{(float)-std::sin(h), (float)std::cos(h)};
    V2 rgt{(float)std::cos(h), (float)std::sin(h)};
    return (fwd * forward + rgt * right).Normalized();
}

// ------------------------------------------------------------------ recoil

SprayPoint SprayRecoil::OnShot(const Weapon& w, float now, float inaccuracy, const std::function<float()>& rand01) {
    if (weaponId_ != w.id) { weaponId_ = w.id; shotIndex_ = 0; }
    const auto& p = w.pattern;
    int k = (int)std::lround(shotIndex_);
    float dPitch, dYaw;
    if (k + 1 < (int)p.size()) {
        dPitch = p[k + 1].pitch - p[k].pitch;
        dYaw = p[k + 1].yaw - p[k].yaw;
    } else {
        dPitch = 0.15f * w.recoilScale;
        dYaw = (rand01() - 0.5f) * 1.2f * w.recoilScale;
    }
    if (inaccuracy > 0.f) {
        double angle = rand01() * 6.283185307;
        float radius = std::sqrt(rand01()) * w.moveInaccuracy * inaccuracy;
        dPitch += radius * (float)std::sin(angle);
        dYaw += radius * (float)std::cos(angle);
    }
    shotIndex_ += 1.f;
    lastShot_ = now;
    punchPitch_ += dPitch;
    punchYaw_ += dYaw;
    return {dYaw, dPitch};
}

SprayPoint SprayRecoil::Update(const Weapon* w, float now, float dt) {
    if (!w || dt <= 0.f) return {};
    if (now - lastShot_ < w->cycleTime * 1.2f) return {};
    if (shotIndex_ > 0.f)
        shotIndex_ = std::fmax(0.f, shotIndex_ - std::fmax(1.f, (float)w->pattern.size()) / w->RecoveryTime() * dt);
    float np = Decay(punchPitch_, dt), ny = Decay(punchYaw_, dt);
    SprayPoint d{ny - punchYaw_, np - punchPitch_};
    punchPitch_ = np;
    punchYaw_ = ny;
    return d;
}

void SprayRecoil::Reset() { shotIndex_ = 0; punchPitch_ = 0; punchYaw_ = 0; lastShot_ = -100; }

float SprayRecoil::Inaccuracy(const Weapon* w, float speed, float maxSpeed, bool onGround, bool scoped) {
    if (!w) return 0.f;
    float f = 0.f;
    if (!onGround) f = 2.5f;
    else if (maxSpeed > 0.f) {
        float frac = speed / maxSpeed;
        if (frac > Movement::kAccurateFraction)
            f = std::fmin(1.f, (frac - Movement::kAccurateFraction) / (1.f - Movement::kAccurateFraction));
    }
    if (w->cls == WeaponClass::Sniper && !scoped) f += 1.f;
    return f;
}

} // namespace csgta
