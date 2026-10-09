// CS:GTA per-frame logic: first-person lock, Counter-Strike movement, guns, recoil and sounds.
#include "mod.hpp"
#include "audio.hpp"
#include "cs_logic.hpp"
#include "game_bridge.hpp"
#include "log.hpp"
#include "natives.hpp"
#include <windows.h>
#include <random>
#include <unordered_map>
#include <utility>
#include <vector>

namespace csgta::mod {
using game::N;
using game::Vec3;
namespace n = nat;

namespace {

Settings S;
bool g_soundsLoaded = false;
std::wstring g_notice;
bool g_enabled = true, g_online = false, g_keyWasDown = false;
float g_time = 0;

Movement g_move;
SprayRecoil g_recoil;
std::mt19937 g_rng{std::random_device{}()};

// movement
V2 g_vel;
bool g_controlling = false, g_wasAirborne = false;
float g_speed = 0, g_landedAt = -100;

// guns
uint32_t g_weapon = 0;
int g_lastClip = -1;
float g_lastShot = -100, g_lastClipDrop = -100, g_reloadStart = -100, g_lastForcedReload = -100, g_nextDamage = 0;
bool g_wasReloading = false, g_wasShooting = false, g_clipInPlayed = false;
const Weapon* g_reloadWeapon = nullptr;
std::unordered_map<uint32_t, int> g_sinceReload;
std::unordered_map<uint32_t, float> g_baseDamage;
std::vector<std::pair<uint32_t, uint32_t>> g_addedSuppressors;

const uint32_t kUnarmed = Joaat("WEAPON_UNARMED");
const uint32_t kSuppressors[] = {Joaat("COMPONENT_AT_PI_SUPP_02"), Joaat("COMPONENT_AT_PI_SUPP"),
                                 Joaat("COMPONENT_AT_AR_SUPP_02"), Joaat("COMPONENT_AT_AR_SUPP"),
                                 Joaat("COMPONENT_AT_SR_SUPP")};

void Notify(const std::wstring& text) {
    std::string utf8(text.begin(), text.end());
    N(n::BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
    N(n::ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, utf8.c_str());
    N(n::END_TEXT_COMMAND_THEFEED_POST_TICKER, false, false);
}

void Disable(int control) { N(n::DISABLE_CONTROL_ACTION, 0, control, true); }
bool B(uint64_t h, int ped) { return N<bool>(h, ped); }

bool PlayerHasControl(int player) {
    if (N<bool>(n::IS_PAUSE_MENU_ACTIVE) || N<bool>(n::GET_IS_LOADING_SCREEN_ACTIVE)) return false;
    if (N<bool>(n::IS_CUTSCENE_PLAYING) || N<bool>(n::IS_PLAYER_SWITCH_IN_PROGRESS)) return false;
    return N<bool>(n::IS_PLAYER_CONTROL_ON, player);
}

void ForceFirstPerson(int ped) {
    Disable(n::kNextCamera);
    Disable(n::kVehicleCinCam);
    N(n::SET_CINEMATIC_BUTTON_ACTIVE, false);
    if (N<bool>(n::IS_PED_IN_ANY_VEHICLE, ped, false)) {
        int ctx = N<int>(n::GET_CAM_ACTIVE_VIEW_MODE_CONTEXT);
        if (N<int>(n::GET_CAM_VIEW_MODE_FOR_CONTEXT, ctx) != 4) N(n::SET_CAM_VIEW_MODE_FOR_CONTEXT, ctx, 4);
    } else if (N<int>(n::GET_FOLLOW_PED_CAM_VIEW_MODE) != 4) {
        N(n::SET_FOLLOW_PED_CAM_VIEW_MODE, 4);
    }
}

bool CanCsMove(int ped, int player) {
    if (!B(n::IS_PED_ON_FOOT, ped) || N<bool>(n::IS_PED_DEAD_OR_DYING, ped, true)) return false;
    if (B(n::IS_PED_RAGDOLL, ped) || B(n::IS_PED_CLIMBING, ped) || B(n::IS_PED_VAULTING, ped)) return false;
    if (N<bool>(n::IS_PED_IN_COVER, ped, false) || B(n::IS_PED_GOING_INTO_COVER, ped)) return false;
    if (B(n::IS_PED_SWIMMING, ped) || B(n::IS_PED_GETTING_INTO_A_VEHICLE, ped) || B(n::IS_PED_IN_MELEE_COMBAT, ped)) return false;
    if (N<int>(n::GET_PED_PARACHUTE_STATE, ped) != -1 || B(n::IS_PED_USING_ANY_SCENARIO, ped)) return false;
    if (N<bool>(n::GET_IS_TASK_ACTIVE, ped, n::kTaskClimbLadder)) return false;
    if (N<bool>(n::IS_PLAYER_BEING_ARRESTED, player, true)) return false;
    return true;
}

void HandleMovement(int ped, int player, const Weapon* w, uint32_t weapon, bool active, float dt) {
    if (!active || !CanCsMove(ped, player)) {
        if (g_controlling) N(n::SET_PED_MOVE_RATE_OVERRIDE, ped, 1.f);
        g_controlling = false;
        g_speed = 0;
        return;
    }
    Vec3 actual = N<Vec3>(n::GET_ENTITY_VELOCITY, ped);
    V2 actualH{actual.x, actual.y};
    bool onGround = !B(n::IS_PED_JUMPING, ped) && !B(n::IS_PED_FALLING, ped) && !B(n::IS_ENTITY_IN_AIR, ped);
    if (onGround && g_wasAirborne) g_landedAt = g_time;
    g_wasAirborne = !onGround;

    if (!g_controlling) { g_vel = actualH; g_controlling = true; }
    else if (g_vel.Length() > 1.f && actualH.Length() < g_vel.Length() * 0.5f && g_time - g_landedAt > 0.3f) g_vel = actualH;

    bool bhop = onGround && g_time - g_landedAt < Movement::kBunnyHopWindow && N<bool>(n::IS_CONTROL_PRESSED, 0, n::kJump);
    float right = N<float>(n::GET_CONTROL_NORMAL, 0, n::kMoveLeftRight);
    float forward = -N<float>(n::GET_CONTROL_NORMAL, 0, n::kMoveUpDown);
    float input = std::fmin(1.f, V2{right, forward}.Length());
    bool walk = false;
    if (S.sprintWalks) {
        Disable(n::kSprint);
        walk = N<bool>(n::IS_DISABLED_CONTROL_PRESSED, 0, n::kSprint);
    }
    bool duck = B(n::GET_PED_STEALTH_MOVEMENT, ped);
    bool armed = weapon != kUnarmed && N<bool>(n::IS_PED_ARMED, ped, 4);
    bool scoped = N<bool>(n::IS_PLAYER_FREE_AIMING, player);

    float maxSpeed = Movement::MaxSpeedMeters(w, armed, scoped);
    float wishSpeed = maxSpeed * input * (duck ? Movement::kDuckFraction : walk ? Movement::kWalkFraction : 1.f);
    Vec3 camRot = N<Vec3>(n::GET_FINAL_RENDERED_CAM_ROT, 2);
    V2 wishDir = Movement::WishDirection(right, forward, camRot.z);

    g_vel = g_move.Step(g_vel, wishDir, wishSpeed, onGround, dt, bhop);
    g_speed = g_vel.Length();
    if (input < 0.05f && g_speed < 0.05f && onGround) {
        g_vel = {};
        N(n::SET_PED_MOVE_RATE_OVERRIDE, ped, 1.f);
        return; // standing still: leave the ped to GTA
    }
    N(n::SET_ENTITY_VELOCITY, ped, g_vel.x, g_vel.y, actual.z);
    N(n::SET_PED_MOVE_RATE_OVERRIDE, ped, std::fmax(0.6f, std::fmin(1.3f, g_speed / 5.2f)));
}

void RefreshDamage() {
    if (g_time < g_nextDamage) return;
    g_nextDamage = g_time + 5.f;
    for (auto& w : Weapons()) {
        auto it = g_baseDamage.find(w.gtaHash);
        if (it == g_baseDamage.end()) {
            float base = N<float>(n::GET_WEAPON_DAMAGE, w.gtaHash, 0);
            if (base <= 0.f) continue;
            it = g_baseDamage.emplace(w.gtaHash, base).first;
        }
        N(n::SET_WEAPON_DAMAGE_MODIFIER, w.gtaHash, w.damage / it->second);
    }
}

void ResetDamage() {
    for (auto& w : Weapons()) N(n::SET_WEAPON_DAMAGE_MODIFIER, w.gtaHash, 1.f);
    g_nextDamage = 0;
}

void EnsureSuppressor(int ped, const Weapon& w) {
    if (!S.csSounds || !S.quietGtaGunfire || !audio::Has(w.id)) return;
    for (uint32_t comp : kSuppressors) {
        if (!N<bool>(n::DOES_WEAPON_TAKE_WEAPON_COMPONENT, w.gtaHash, comp)) continue;
        if (!N<bool>(n::HAS_PED_GOT_WEAPON_COMPONENT, ped, w.gtaHash, comp)) {
            N(n::GIVE_WEAPON_COMPONENT_TO_PED, ped, w.gtaHash, comp);
            g_addedSuppressors.push_back({w.gtaHash, comp});
        }
        return;
    }
}

void RemoveSuppressors() {
    int ped = N<int>(n::PLAYER_PED_ID);
    for (auto& [weapon, comp] : g_addedSuppressors) N(n::REMOVE_WEAPON_COMPONENT_FROM_PED, ped, weapon, comp);
    g_addedSuppressors.clear();
}

void Kick(SprayPoint d) {
    if (d.pitch == 0.f && d.yaw == 0.f) return;
    N(n::SET_GAMEPLAY_CAM_RELATIVE_PITCH, N<float>(n::GET_GAMEPLAY_CAM_RELATIVE_PITCH) + d.pitch, 1.f);
    N(n::SET_GAMEPLAY_CAM_RELATIVE_HEADING, N<float>(n::GET_GAMEPLAY_CAM_RELATIVE_HEADING) - d.yaw); // GTA turns counter-clockwise
}

void HandleGun(int ped, int player, const Weapon* w, uint32_t weapon, float dt) {
    int clip = 0;
    N<bool>(n::GET_AMMO_IN_CLIP, ped, weapon, &clip);
    bool reloading = B(n::IS_PED_RELOADING, ped);
    bool shooting = B(n::IS_PED_SHOOTING, ped);

    if (weapon != g_weapon) {
        g_weapon = weapon;
        g_lastClip = clip;
        g_wasReloading = reloading;
        g_wasShooting = shooting;
        g_recoil.Reset();
        if (w) EnsureSuppressor(ped, *w);
        return;
    }
    if (w && reloading && !g_wasReloading) {
        g_sinceReload[weapon] = 0;
        g_reloadStart = g_time;
        g_reloadWeapon = w;
        g_clipInPlayed = false;
        if (S.csSounds) audio::PlayClipOut(w->id);
    }
    g_wasReloading = reloading;
    if (g_reloadWeapon && g_reloadWeapon == w && !g_clipInPlayed && g_time - g_reloadStart > w->reloadTime * 0.6f) {
        g_clipInPlayed = true;
        if (S.csSounds) audio::PlayClipIn(w->id);
    }

    int shots = 0;
    if (g_lastClip >= 0 && clip < g_lastClip && !reloading) { shots = std::min(g_lastClip - clip, 5); g_lastClipDrop = g_time; }
    else if (clip > g_lastClip) g_sinceReload[weapon] = 0;
    else if (shooting && !g_wasShooting && g_time - g_lastClipDrop > 1.f) shots = 1; // infinite-ammo cheats
    g_lastClip = clip;
    g_wasShooting = shooting;
    if (!w) return;

    if (shots > 0) {
        bool onGround = !B(n::IS_PED_JUMPING, ped) && !B(n::IS_PED_FALLING, ped);
        bool scoped = N<bool>(n::IS_PLAYER_FREE_AIMING, player);
        float speed = g_controlling ? g_speed : [&] { Vec3 v = N<Vec3>(n::GET_ENTITY_VELOCITY, ped); return V2{v.x, v.y}.Length(); }();
        float inacc = SprayRecoil::Inaccuracy(w, speed, Movement::MaxSpeedMeters(w, true, scoped), onGround, scoped);
        std::uniform_real_distribution<float> u(0.f, 1.f);
        for (int i = 0; i < shots; i++) {
            if (S.csSounds) audio::PlayShot(w->id);
            if (S.sprayRecoil) Kick(g_recoil.OnShot(*w, g_time, inacc, [&] { return u(g_rng); }));
        }
        g_sinceReload[weapon] += shots;
        g_lastShot = g_time;
    } else if (S.sprayRecoil) {
        Kick(g_recoil.Update(w, g_time, dt));
    }

    // Counter-Strike fire rate, magazine size and reload time.
    bool block = g_time - g_lastShot < w->cycleTime - 0.01f;
    if (g_reloadWeapon == w && g_time - g_reloadStart < w->reloadTime) block = true;
    if (g_sinceReload[weapon] >= w->magSize) {
        block = true;
        int total = N<int>(n::GET_AMMO_IN_PED_WEAPON, ped, weapon);
        if (!reloading && clip > 0 && total > clip && g_time - g_lastForcedReload > 1.f) {
            g_lastForcedReload = g_time;
            N<bool>(n::MAKE_PED_RELOAD, ped);
        }
    }
    if (block) { Disable(n::kAttack); Disable(n::kAttack2); }
}

void TurnOff() {
    ResetDamage();
    RemoveSuppressors();
    N(n::SET_PED_MOVE_RATE_OVERRIDE, N<int>(n::PLAYER_PED_ID), 1.f);
    N(n::SET_CINEMATIC_BUTTON_ACTIVE, true);
    g_controlling = false;
    g_weapon = 0;
}

} // namespace

void Configure(const Settings& s, bool soundsLoaded, const std::wstring& notice) {
    S = s;
    g_soundsLoaded = soundsLoaded;
    g_notice = notice;
}

void Tick() {
    float dt = std::fmin(0.1f, std::fmax(0.f, N<float>(n::GET_FRAME_TIME)));
    g_time += dt;

    // Story mode only: stand down completely whenever GTA Online is running.
    bool online = N<bool>(n::NETWORK_IS_SESSION_STARTED);
    if (online != g_online) {
        g_online = online;
        if (online) { TurnOff(); Log(L"GTA Online detected: CS:GTA is off."); }
    }
    if (g_online) return;

    if (!g_notice.empty()) { Notify(g_notice); g_notice.clear(); }

    bool keyDown = (GetAsyncKeyState(S.toggleKey) & 0x8000) != 0;
    if (keyDown && !g_keyWasDown) {
        g_enabled = !g_enabled;
        if (!g_enabled) TurnOff();
        Notify(g_enabled ? L"CS:GTA ~g~on" : L"CS:GTA ~r~off");
    }
    g_keyWasDown = keyDown;
    if (!g_enabled) return;

    int player = N<int>(n::PLAYER_ID);
    int ped = N<int>(n::PLAYER_PED_ID);
    if (!N<bool>(n::DOES_ENTITY_EXIST, ped)) return;

    bool active = PlayerHasControl(player);
    if (S.forceFirstPerson && active) ForceFirstPerson(ped);

    uint32_t weapon = N<uint32_t>(n::GET_SELECTED_PED_WEAPON, ped);
    const Weapon* w = S.csGuns ? WeaponByGtaHash(weapon) : nullptr;
    if (S.csGuns) {
        RefreshDamage();
        HandleGun(ped, player, w, weapon, dt);
    }
    if (S.csMovement) HandleMovement(ped, player, w, weapon, active, dt);
}

void Shutdown() {
    if (!g_online) TurnOff();
}

} // namespace csgta::mod
