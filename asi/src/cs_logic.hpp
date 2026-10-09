// Counter-Strike 2 rules for CS:GTA: gun table, spray patterns, movement and recoil.
// Plain C++ with no game or Windows dependencies, so it is unit-tested on any machine.
#pragma once
#include <cmath>
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

namespace csgta {

uint32_t Joaat(const std::string& text);

enum class WeaponClass { Pistol, Smg, Rifle, Sniper, Shotgun, MachineGun };

struct SprayPoint { float yaw = 0, pitch = 0; };

struct Weapon {
    std::string id;          // sound folder under %LOCALAPPDATA%\CSGTA\sounds
    std::string name;        // Counter-Strike name
    std::string gtaWeapon;   // e.g. WEAPON_ASSAULTRIFLE
    WeaponClass cls;
    float damage;            // per bullet / per pellet
    float cycleTime;         // seconds between shots
    int magSize;
    float reloadTime;        // seconds
    float maxSpeed;          // units/s while holding it
    float scopedSpeed;       // units/s while scoped (0 = same)
    float recoilScale;
    float moveInaccuracy;    // degrees of aim kick at full speed
    uint32_t gtaHash = 0;
    std::vector<SprayPoint> pattern; // cumulative offsets, one per bullet

    float RecoveryTime() const { return std::fmax(0.3f, std::fmin(1.2f, cycleTime * 6.f)); }
};

constexpr float kUnitsToMeters = 0.0254f;
constexpr float kKnifeSpeed = 250.f;
constexpr float kUnmappedGunSpeed = 220.f;

const std::vector<Weapon>& Weapons();
const Weapon* WeaponByGtaHash(uint32_t hash);

struct V2 {
    float x = 0, y = 0;
    float Length() const { return std::sqrt(x * x + y * y); }
    V2 operator+(V2 o) const { return {x + o.x, y + o.y}; }
    V2 operator*(float s) const { return {x * s, y * s}; }
    static float Dot(V2 a, V2 b) { return a.x * b.x + a.y * b.y; }
    V2 Normalized() const { float l = Length(); return l > 1e-5f ? V2{x / l, y / l} : V2{}; }
};

// Source-engine ground/air movement with Counter-Strike 2's default tuning, in metres per second.
struct Movement {
    float accelerate = 5.5f, airAccelerate = 12.f, friction = 5.2f;
    float stopSpeed = 80.f * kUnitsToMeters, airWishCap = 30.f * kUnitsToMeters;
    static constexpr float kWalkFraction = 0.52f, kDuckFraction = 0.34f, kAccurateFraction = 0.34f;
    static constexpr float kBunnyHopWindow = 0.1f; // jump again this soon after landing: no friction, no speed cap

    V2 Step(V2 v, V2 wishDir, float wishSpeed, bool onGround, float dt, bool skipFriction = false) const;
    static float MaxSpeedMeters(const Weapon* w, bool armed, bool scoped);
    static V2 WishDirection(float inputRight, float inputForward, float cameraHeadingDeg);
};

// One spray: which bullet of the pattern is next, how far aim was pushed, and settling back.
class SprayRecoil {
public:
    static constexpr float kDecayExp = 8.f, kDecayLinear = 18.f;
    SprayPoint OnShot(const Weapon& w, float now, float inaccuracy, const std::function<float()>& rand01);
    SprayPoint Update(const Weapon* w, float now, float dt);
    void Reset();
    float ShotIndex() const { return shotIndex_; }
    float PunchPitch() const { return punchPitch_; }
    static float Inaccuracy(const Weapon* w, float speed, float maxSpeed, bool onGround, bool scoped);
private:
    float shotIndex_ = 0, lastShot_ = -100, punchPitch_ = 0, punchYaw_ = 0;
    std::string weaponId_;
};

} // namespace csgta
