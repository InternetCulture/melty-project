#pragma once
#include <string>

namespace csgta {

void InitLog(const std::wstring& path);
void Log(const std::wstring& message);
inline void Log(const std::string& message) { Log(std::wstring(message.begin(), message.end())); }

} // namespace csgta
