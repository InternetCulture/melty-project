// CS:GTA plugin entry point, loaded by Ultimate ASI Loader at game start.
#include "audio.hpp"
#include "game_bridge.hpp"
#include "log.hpp"
#include "mod.hpp"
#include <windows.h>
#include <shlobj.h>
#include <cstdio>
#include <fstream>
#include <mutex>

namespace csgta {
namespace {

std::mutex g_logLock;
std::wstring g_logPath;
HMODULE g_module = nullptr;

std::wstring ModuleDir() {
    wchar_t path[MAX_PATH] = {};
    GetModuleFileNameW(g_module, path, MAX_PATH);
    std::wstring p(path);
    return p.substr(0, p.find_last_of(L"\\/"));
}

std::wstring LocalAppData() {
    wchar_t* p = nullptr;
    std::wstring r;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &p))) r = p;
    CoTaskMemFree(p);
    return r;
}

bool IniBool(const std::wstring& ini, const wchar_t* sec, const wchar_t* key, bool def) {
    wchar_t buf[16];
    GetPrivateProfileStringW(sec, key, def ? L"true" : L"false", buf, 16, ini.c_str());
    return buf[0] == L't' || buf[0] == L'T' || buf[0] == L'1' || buf[0] == L'y' || buf[0] == L'Y';
}

float IniFloat(const std::wstring& ini, const wchar_t* sec, const wchar_t* key, float def) {
    wchar_t buf[32];
    GetPrivateProfileStringW(sec, key, L"", buf, 32, ini.c_str());
    return buf[0] ? (float)_wtof(buf) : def;
}

int KeyFromName(const std::wstring& name, int def) {
    if (name.size() >= 2 && (name[0] == L'F' || name[0] == L'f')) {
        int n = _wtoi(name.c_str() + 1);
        if (n >= 1 && n <= 24) return VK_F1 + n - 1;
    }
    return def;
}

DWORD WINAPI Start(LPVOID) {
    std::wstring dir = ModuleDir();
    std::wstring data = LocalAppData() + L"\\CSGTA";
    CreateDirectoryW(data.c_str(), nullptr);
    InitLog(data + L"\\CSGTA.log");
    Log(L"CS:GTA starting.");

    std::wstring ini = dir + L"\\CSGTA\\CSGTA.ini";
    mod::Settings s;
    s.forceFirstPerson = IniBool(ini, L"Camera", L"ForceFirstPerson", true);
    s.csMovement = IniBool(ini, L"Movement", L"CounterStrikeMovement", true);
    s.sprintWalks = IniBool(ini, L"Movement", L"SprintKeyWalks", true);
    s.csGuns = IniBool(ini, L"Guns", L"CounterStrikeGuns", true);
    s.sprayRecoil = IniBool(ini, L"Guns", L"SprayRecoil", true);
    s.csSounds = IniBool(ini, L"Sounds", L"CounterStrikeSounds", true);
    s.quietGtaGunfire = IniBool(ini, L"Sounds", L"QuietGtaGunfire", true);
    s.volume = IniFloat(ini, L"Sounds", L"Volume", 0.8f);
    wchar_t key[16];
    GetPrivateProfileStringW(L"General", L"ToggleKey", L"F10", key, 16, ini.c_str());
    s.toggleKey = KeyFromName(key, VK_F10);

    // Counter-Strike 2 sounds were copied from the player's own CS2 by CsSoundImporter (Melty runs it first).
    int guns = 0;
    if (s.csSounds && audio::Start(s.volume)) guns = audio::Load(data + L"\\sounds");
    std::wstring notice = L"CS:GTA ~g~on~s~ (F10).";
    if (s.csSounds) notice += guns > 0 ? L" Counter-Strike 2 sounds for " + std::to_wstring(guns) + L" guns."
                                       : L" No Counter-Strike 2 sounds found; GTA gun sounds stay on.";
    Log(L"Loaded Counter-Strike 2 sounds for " + std::to_wstring(guns) + L" guns.");
    mod::Configure(s, guns > 0, notice);

    std::string why;
    if (!game::Connect(why)) {
        Log("CS:GTA is inactive: " + why + ". The game plays as normal.");
        return 0;
    }
    Log(std::string("Connected to ") + game::TargetBuild());
    game::RunEachFrame(&mod::Tick);
    return 0;
}

} // namespace

void InitLog(const std::wstring& path) {
    std::lock_guard<std::mutex> l(g_logLock);
    g_logPath = path;
}

void Log(const std::wstring& message) {
    std::lock_guard<std::mutex> l(g_logLock);
    if (g_logPath.empty()) return;
    FILE* f = _wfopen(g_logPath.c_str(), L"a, ccs=UTF-8");
    if (!f) return;
    SYSTEMTIME t;
    GetLocalTime(&t);
    fwprintf(f, L"%04d-%02d-%02d %02d:%02d:%02d  %ls\n", t.wYear, t.wMonth, t.wDay, t.wHour, t.wMinute, t.wSecond, message.c_str());
    fclose(f);
}

} // namespace csgta

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        csgta::g_module = instance;
        DisableThreadLibraryCalls(instance);
        if (HANDLE t = CreateThread(nullptr, 0, csgta::Start, nullptr, 0, nullptr)) CloseHandle(t);
    } else if (reason == DLL_PROCESS_DETACH) {
        csgta::audio::Stop();
    }
    return TRUE;
}
