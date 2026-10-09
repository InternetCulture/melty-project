// Plays the Counter-Strike 2 gun sounds imported from the player's own CS2 install.
#pragma once
#include <string>

namespace csgta::audio {

bool Start(float volume);        // opens the Windows audio output
int Load(const std::wstring& soundsDir); // %LOCALAPPDATA%\CSGTA\sounds\<gun>\shot_N.wav, clipout.wav, clipin.wav
bool Has(const std::string& gunId);
void PlayShot(const std::string& gunId);
void PlayClipOut(const std::string& gunId);
void PlayClipIn(const std::string& gunId);
void Stop();

} // namespace csgta::audio
