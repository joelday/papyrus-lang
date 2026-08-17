#pragma once

#define SPDLOG_LEVEL_NAMES { "TRACE", "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL", "OFF" }

#include <SFSE/Impl/PCH.h>
#undef GetObject
#undef GetObjectA
#include <SFSE/SFSE.h>
#include <SFSE/API.h>
#include <SFSE/Interfaces.h>
#include <SFSE/Logger.h>
#include <SFSE/Trampoline.h>
#include <spdlog/sinks/basic_file_sink.h>

namespace DarkId::Papyrus::DebugServer
{
	using namespace std::literals;
	namespace XSE = SFSE;
	// CommonLibSF's SFSE::log only exposes log_directory(); file logging is spdlog.
	namespace logger = spdlog;
}

#define STACK_FRAME_IP ip
namespace XSE = SFSE;

namespace stl
{
	using namespace XSE::stl;

	template <std::size_t N, class T>
	void write_thunk_call(std::uintptr_t a_src)
	{
		auto& trampoline = XSE::GetTrampoline();
		XSE::AllocTrampoline(14);
		T::func = trampoline.write_call<N>(a_src, T::thunk);
	}

	template <std::size_t N, class T>
	void write_thunk_branch(std::uintptr_t a_src)
	{
		auto& trampoline = XSE::GetTrampoline();
		XSE::AllocTrampoline(14);
		T::func = trampoline.write_branch<N>(a_src, T::thunk);
	}
}

#define DLLEXPORT __declspec(dllexport)
