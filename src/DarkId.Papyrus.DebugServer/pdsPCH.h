#pragma once
#if SKYRIM
#define SPDLOG_LEVEL_NAMES { "TRACE", "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL", "OFF" }
#include <SKSE/Impl/PCH.h>
#undef GetObject // Have to do this because PCH pulls in spdlog->winbase.h->windows.h->wingdi.h, which redfines GetObject
#undef GetObjectA
#include <RE/Skyrim.h>
#include <SKSE/SKSE.h>
#include <SKSE/API.h>
#include <SKSE/Logger.h>
#include <spdlog/sinks/basic_file_sink.h>

namespace DarkId::Papyrus::DebugServer
{
  using namespace std::literals;
  namespace XSE = SKSE;
  namespace logger = SKSE::log;
}
#define DLLEXPORT __declspec(dllexport)

#elif FALLOUT

#include <F4SE/Impl/PCH.h>
#undef GetObject // Have to do this because PCH pulls in spdlog->winbase.h->windows.h->wingdi.h, which redfines GetObject
#undef GetObjectA
#include <RE/Fallout.h>
#include <F4SE/F4SE.h>
#include <F4SE/API.h>
#include <F4SE/Logger.h>
#include <spdlog/sinks/basic_file_sink.h>

namespace DarkId::Papyrus::DebugServer
{
  using namespace std::literals;
  namespace XSE = F4SE;
  namespace logger = F4SE::log;
}
#define DLLEXPORT __declspec(dllexport)

#endif