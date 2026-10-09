#pragma once
#include <string>

namespace csgta::mod {

struct Settings {
    bool forceFirstPerson = true, csMovement = true, sprintWalks = true, csGuns = true, sprayRecoil = true;
    bool csSounds = true, quietGtaGunfire = true;
    float volume = 0.8f;
    int toggleKey = 0x79; // VK_F10
};

void Configure(const Settings& s, bool soundsLoaded, const std::wstring& startupNotice);
void Tick();      // once per frame, on the game's script thread
void Shutdown();  // undo damage modifiers and fitted suppressors

} // namespace csgta::mod
