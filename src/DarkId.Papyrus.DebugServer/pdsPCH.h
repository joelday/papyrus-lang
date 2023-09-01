#pragma once

#define SPDLOG_LEVEL_NAMES { "TRACE", "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL", "OFF" }
#if SKYRIM
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
namespace XSE = SKSE;

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
namespace XSE = F4SE;
#endif

namespace stl {
	using namespace XSE::stl;

	template <class T>
	void write_thunk_call(std::uintptr_t a_src)
	{
		auto& trampoline = XSE::GetTrampoline();
		XSE::AllocTrampoline(14);

		T::func = trampoline.write_call<5>(a_src, T::thunk);
	}
}

#define DLLEXPORT __declspec(dllexport)
