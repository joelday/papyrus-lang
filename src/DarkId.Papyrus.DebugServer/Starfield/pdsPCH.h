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

// No SFSE::stl / AllocTrampoline copies here — CommonLibSF has neither.
// Instruction trampolines (when unpublished IDs exist) use REL::GetTrampoline
// after SFSE::Init({ .trampoline = true, .trampolineSize = ... }).

#define DLLEXPORT __declspec(dllexport)
