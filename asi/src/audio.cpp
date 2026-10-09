#include "audio.hpp"
#include "log.hpp"
#include <windows.h>
#include <mmsystem.h>
#include <algorithm>
#include <atomic>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <memory>
#include <mutex>
#include <random>
#include <thread>
#include <unordered_map>
#include <vector>

namespace csgta::audio {
namespace {

constexpr int kRate = 44100, kFrames = 1024, kBuffers = 4, kMaxVoices = 24;
using Clip = std::shared_ptr<const std::vector<float>>; // interleaved stereo at kRate

struct Bank { std::vector<Clip> shots; Clip clipOut, clipIn; };
struct Voice { Clip clip; size_t pos = 0; };

std::mutex g_lock;
std::unordered_map<std::string, Bank> g_banks;
std::vector<Voice> g_voices;
std::mt19937 g_rng{12345};
float g_volume = 0.8f;
HWAVEOUT g_out = nullptr;
HANDLE g_event = nullptr;
std::thread g_thread;
std::atomic<bool> g_running{false};
WAVEHDR g_hdr[kBuffers];
int16_t g_pcm[kBuffers][kFrames * 2];

// Reads a PCM 8/16-bit WAV and converts it to stereo float at kRate.
Clip ReadWav(const std::wstring& path) {
    FILE* f = _wfopen(path.c_str(), L"rb");
    if (!f) return nullptr;
    std::vector<uint8_t> b;
    uint8_t chunk[65536];
    for (size_t n; (n = fread(chunk, 1, sizeof chunk, f)) > 0;) b.insert(b.end(), chunk, chunk + n);
    fclose(f);
    if (b.size() < 44 || std::memcmp(b.data(), "RIFF", 4) || std::memcmp(b.data() + 8, "WAVE", 4)) return nullptr;
    uint16_t fmt = 0, ch = 0, bits = 0;
    uint32_t rate = 0;
    const uint8_t* data = nullptr;
    uint32_t dataLen = 0;
    for (size_t p = 12; p + 8 <= b.size();) {
        uint32_t len;
        std::memcpy(&len, b.data() + p + 4, 4);
        if (!std::memcmp(b.data() + p, "fmt ", 4) && len >= 16) {
            std::memcpy(&fmt, b.data() + p + 8, 2);
            std::memcpy(&ch, b.data() + p + 10, 2);
            std::memcpy(&rate, b.data() + p + 12, 4);
            std::memcpy(&bits, b.data() + p + 22, 2);
        } else if (!std::memcmp(b.data() + p, "data", 4)) {
            data = b.data() + p + 8;
            dataLen = (uint32_t)std::min<size_t>(len, b.size() - p - 8);
        }
        p += 8 + len + (len & 1);
    }
    if (!data || fmt != 1 || (bits != 8 && bits != 16) || ch < 1 || ch > 2 || rate == 0) {
        Log(L"Skipped unsupported sound " + path);
        return nullptr;
    }
    size_t bytesPer = bits / 8, frames = dataLen / (bytesPer * ch);
    std::vector<float> src(frames * 2);
    for (size_t i = 0; i < frames; i++)
        for (int c = 0; c < 2; c++) {
            size_t at = (i * ch + (ch == 2 ? c : 0)) * bytesPer;
            float s = bits == 16 ? (int16_t)(data[at] | (data[at + 1] << 8)) / 32768.f : (data[at] - 128) / 128.f;
            src[i * 2 + c] = s;
        }
    if (rate == (uint32_t)kRate) return std::make_shared<const std::vector<float>>(std::move(src));
    double step = (double)rate / kRate;
    size_t outFrames = (size_t)(frames / step);
    std::vector<float> out(outFrames * 2);
    for (size_t i = 0; i < outFrames; i++) {
        double pos = i * step;
        size_t a = (size_t)pos, bIdx = std::min(a + 1, frames - 1);
        float t = (float)(pos - a);
        for (int c = 0; c < 2; c++) out[i * 2 + c] = src[a * 2 + c] * (1 - t) + src[bIdx * 2 + c] * t;
    }
    return std::make_shared<const std::vector<float>>(std::move(out));
}

void Mix(int16_t* pcm) {
    float acc[kFrames * 2] = {};
    {
        std::lock_guard<std::mutex> l(g_lock);
        for (auto& v : g_voices) {
            size_t n = std::min<size_t>(kFrames * 2, v.clip->size() - v.pos);
            for (size_t i = 0; i < n; i++) acc[i] += (*v.clip)[v.pos + i];
            v.pos += n;
        }
        g_voices.erase(std::remove_if(g_voices.begin(), g_voices.end(),
                                      [](const Voice& v) { return v.pos >= v.clip->size(); }),
                       g_voices.end());
    }
    for (int i = 0; i < kFrames * 2; i++) {
        float s = acc[i] * g_volume;
        s = s > 1.f ? 1.f : s < -1.f ? -1.f : s;
        pcm[i] = (int16_t)(s * 32767.f);
    }
}

void Pump() {
    while (g_running) {
        WaitForSingleObject(g_event, 50);
        for (auto& h : g_hdr)
            if (h.dwFlags & WHDR_DONE) {
                Mix(reinterpret_cast<int16_t*>(h.lpData));
                h.dwFlags &= ~WHDR_DONE;
                waveOutWrite(g_out, &h, sizeof(WAVEHDR));
            }
    }
}

void Play(const Clip& c) {
    if (!c || !g_out) return;
    std::lock_guard<std::mutex> l(g_lock);
    if (g_voices.size() >= (size_t)kMaxVoices) g_voices.erase(g_voices.begin());
    g_voices.push_back({c, 0});
}

} // namespace

bool Start(float volume) {
    g_volume = volume;
    WAVEFORMATEX wf{};
    wf.wFormatTag = WAVE_FORMAT_PCM;
    wf.nChannels = 2;
    wf.nSamplesPerSec = kRate;
    wf.wBitsPerSample = 16;
    wf.nBlockAlign = 4;
    wf.nAvgBytesPerSec = kRate * 4;
    g_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (waveOutOpen(&g_out, WAVE_MAPPER, &wf, (DWORD_PTR)g_event, 0, CALLBACK_EVENT) != MMSYSERR_NOERROR) {
        g_out = nullptr;
        Log(L"Could not open audio output.");
        return false;
    }
    for (int i = 0; i < kBuffers; i++) {
        std::memset(g_pcm[i], 0, sizeof g_pcm[i]);
        g_hdr[i] = {};
        g_hdr[i].lpData = reinterpret_cast<LPSTR>(g_pcm[i]);
        g_hdr[i].dwBufferLength = sizeof g_pcm[i];
        waveOutPrepareHeader(g_out, &g_hdr[i], sizeof(WAVEHDR));
        waveOutWrite(g_out, &g_hdr[i], sizeof(WAVEHDR));
    }
    g_running = true;
    g_thread = std::thread(Pump);
    return true;
}

int Load(const std::wstring& dir) {
    std::unordered_map<std::string, Bank> banks;
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW((dir + L"\\*").c_str(), &fd);
    if (h == INVALID_HANDLE_VALUE) return 0;
    do {
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) || fd.cFileName[0] == L'.') continue;
        std::wstring gunDir = dir + L"\\" + fd.cFileName;
        Bank bank;
        for (int i = 1; i <= 8; i++)
            if (auto c = ReadWav(gunDir + L"\\shot_" + std::to_wstring(i) + L".wav")) bank.shots.push_back(c);
        bank.clipOut = ReadWav(gunDir + L"\\clipout.wav");
        bank.clipIn = ReadWav(gunDir + L"\\clipin.wav");
        std::wstring w(fd.cFileName);
        banks[std::string(w.begin(), w.end())] = std::move(bank);
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    int guns = 0;
    for (auto& kv : banks) guns += kv.second.shots.empty() ? 0 : 1;
    std::lock_guard<std::mutex> l(g_lock);
    g_banks = std::move(banks);
    return guns;
}

bool Has(const std::string& id) {
    std::lock_guard<std::mutex> l(g_lock);
    auto it = g_banks.find(id);
    return it != g_banks.end() && !it->second.shots.empty();
}

void PlayShot(const std::string& id) {
    Clip c;
    {
        std::lock_guard<std::mutex> l(g_lock);
        auto it = g_banks.find(id);
        if (it == g_banks.end() || it->second.shots.empty()) return;
        c = it->second.shots[g_rng() % it->second.shots.size()];
    }
    Play(c);
}

void PlayClipOut(const std::string& id) {
    Clip c;
    { std::lock_guard<std::mutex> l(g_lock); auto it = g_banks.find(id); if (it != g_banks.end()) c = it->second.clipOut; }
    Play(c);
}

void PlayClipIn(const std::string& id) {
    Clip c;
    { std::lock_guard<std::mutex> l(g_lock); auto it = g_banks.find(id); if (it != g_banks.end()) c = it->second.clipIn; }
    Play(c);
}

void Stop() {
    if (!g_running) return;
    g_running = false;
    SetEvent(g_event);
    if (g_thread.joinable()) g_thread.join();
    waveOutReset(g_out);
    for (auto& hdr : g_hdr) waveOutUnprepareHeader(g_out, &hdr, sizeof(WAVEHDR));
    waveOutClose(g_out);
    g_out = nullptr;
}

} // namespace csgta::audio
