#if SKYRIM
#include <SKSE/Version.h>
#include <SKSE/Trampoline.h>
#include <SKSE/API.h>
#include <SKSE/Logger.h>
#include <ShlObj.h>
#include <string_view>
namespace XSE = SKSE;

#elif FALLOUT
#include <F4SE/Logger.h>
#include <F4SE/API.h>

namespace XSE = F4SE;

#endif

#include "version.h"  // VERSION_VERSTRING, VERSION_MAJOR

#include "DebugServer.h"
#include "ConfigHooks.h"
#include "RuntimeEvents.h"
#include "AddressLibraryIds.h"

#include <Windows.h>
#include <filesystem>
#include <vector>

using namespace DarkId::Papyrus::DebugServer;

DebugServer* g_debugServer;
using namespace std::literals;

#ifdef SKYRIM
    constexpr auto gameLoaded = SKSE::MessagingInterface::kDataLoaded;
#else
    constexpr auto gameLoaded = F4SE::MessagingInterface::kGameLoaded;
#endif


void MessageHandler(XSE::MessagingInterface::Message* msg)
{
	switch (msg->type)
	{
		case gameLoaded:
		{
			RuntimeEvents::Internal::CommitHooks();

			g_debugServer->Listen();
			logger::info("Listening for connections from adapter messaging proxy...");

			break;
		}
	}
}

static bool log_initialized = false;
bool InitializeLog()
{
	if (log_initialized) {
		return true;
	}
	auto path = logger::log_directory();
	if (!path) {
		//XSE::stl::report_and_fail("Failed to find standard logging directory"sv); // Doesn't work in VR
	}
	*path /= "DarkId.Papyrus.DebugServer.log"sv;
	auto sink = std::make_shared<spdlog::sinks::basic_file_sink_mt>(path->string(), true);

	auto log = std::make_shared<spdlog::logger>("global log"s, std::move(sink));
	log->set_level(spdlog::level::debug);
#if _DEBUG
	log->flush_on(spdlog::level::debug);
#else
	log->flush_on(spdlog::level::err);
#endif
	spdlog::set_default_logger(std::move(log));
	spdlog::set_pattern("%H:%M:%S,%e %l %@: %v"s);
	logger::info("Papyrus Debug Server v{}"sv, DIDPDS_VERSION_SEMVER);
	log_initialized = true;
	return true;
}

#if SKYRIM
extern "C" DLLEXPORT constinit auto SKSEPlugin_Version = []() {
	SKSE::PluginVersionData v;
	v.PluginVersion(DIDPDS_VERSION_MAJOR);
	v.PluginName("Papyrus Debug Server");
	v.AuthorName("Joel Day");
	v.UsesAddressLibrary(true);
	v.UsesSigScanning(true);
	v.UsesNoStructs(true);
	return v;
}();
#endif
//#define _PAUSE_ON_START 1

extern "C"
{
#if SKYRIM
	DLLEXPORT bool SKSEPlugin_Query(const XSE::QueryInterface* a_xse, XSE::PluginInfo* a_info)
#elif FALLOUT
	DLLEXPORT bool F4SEPlugin_Query(const XSE::QueryInterface* a_xse, XSE::PluginInfo* a_info)
#endif
	{
		// SKSEPlugin_Query does not get called by certain versions of skse64 if everything necessary in SKSEPlugin_Version is there,
		// so we have to check if it was initialized
		if (!log_initialized) {
			InitializeLog();
		}
		a_info->infoVersion = 1;

		a_info->name = "Papyrus Debug Server";
		a_info->version = DIDPDS_VERSION_MAJOR;

		if (a_xse->IsEditor()) {
			logger::critical("Loaded in editor, marking as incompatible!\n");
			return false;
		}
		// Runtime compatibility is the versioned Address Library JSON loaded in Plugin_Load.
		return true;
	}
}

static std::filesystem::path PluginDirectory()
{
	HMODULE module = nullptr;
	GetModuleHandleExW(
		GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
		reinterpret_cast<LPCWSTR>(&PluginDirectory),
		&module);
	wchar_t path[MAX_PATH]{};
	GetModuleFileNameW(module, path, MAX_PATH);
	return std::filesystem::path(path).parent_path();
}

static bool LoadAddressLibraryOrFail(const XSE::LoadInterface* a_xse)
{
#ifdef SKYRIM
	const char* game = REL::Module::IsVR() ? "skyrimvr" : "skyrimse";
	const char* flavor = REL::Module::IsAE() ? "ae" : (REL::Module::IsVR() ? "vr" : "se");
	const std::vector<std::string_view> required = {
		"pPapyrusEnableLogging",
		"pPapyrusEnableTrace",
		"pPapyrusLoadDebugInformation",
		"pControlsBackgroundMouse",
		"pControlsBackgroundMouseDynamicInitializer",
		"InitWindows",
		"bsrendererBegin",
		"GetInstructionNumberForOffset",
		"VM_ctor",
		"CodeTaskletProcess",
		"CreateStack",
		"CleanupStack",
	};
#else
	const char* game = "fallout4";
	const char* flavor = "f4";
	const std::vector<std::string_view> required = {
		"pPapyrusEnableLogging",
		"pPapyrusEnableTrace",
		"pControlsBackgroundMouse",
		"pControlsBackgroundMouseDynamicInitializer",
		"GetInstructionNumberForOffset",
		"VM_ctor",
		"CodeTaskletProcess",
		"CreateStack",
		"CleanupStack",
		"pCurrentRenderWindow",
	};
#endif
	const auto version = a_xse->RuntimeVersion().string();
	const auto pluginDir = PluginDirectory();
	const std::vector<std::filesystem::path> search = {
		pluginDir / "data",
		pluginDir,
	};

	AddressLibraryIds::LoadError error;
	if (!AddressLibraryIds::Load(game, version, flavor, search, required, error))
	{
		logger::critical("{}", error.message);
		XSE::stl::report_and_fail(error.message);
	}

	logger::info(
		"Loaded Address Library IDs for {} {} ({}) from {}"sv,
		AddressLibraryIds::LoadedGame(),
		AddressLibraryIds::LoadedVersion(),
		AddressLibraryIds::LoadedFlavor(),
		AddressLibraryIds::LoadedPath().string());
	return true;
}

extern "C"
{
#if SKYRIM
	DLLEXPORT bool SKSEPlugin_Load(const XSE::LoadInterface* a_xse)
#elif FALLOUT
	DLLEXPORT bool F4SEPlugin_Load(const XSE::LoadInterface* a_xse)
#endif

//#define _PAUSE_ON_START 1
	{
		if (!log_initialized) {
			InitializeLog();
		}
		logger::info("Papyrus Debug Server loaded");
 #if _DEBUG && _PAUSE_ON_START
 		logger::info("Waiting for debugger to attach...");

 		while (!IsDebuggerPresent())
 		{
 			Sleep(10);
 		}

 		Sleep(1000 * 4);
 		logger::info("Debugger attached!");
 #endif

		g_debugServer = new DebugServer();
		logger::info("Initializing plugin...");
		Init(a_xse);
		logger::info("Plugin Initialized!");

		LoadAddressLibraryOrFail(a_xse);

		// We install this hook before any scripts are loaded to ensure that any script loads are done with LoadDebugInformation enabled.
		logger::info("Installing EnableLoadDebugInformation hook...");
		EnableLoadDebugInformation::Install();
		logger::info("Installing EnableLoadDebugInformation hook installed!");
#if SKYRIM
		logger::info("Registering Listener...");
		if (XSE::GetMessagingInterface()->RegisterListener("SKSE", MessageHandler)){
			logger::info("Registered Listener!");
		} else {
			logger::critical("Failed to register listener!!");
		}
#elif FALLOUT
		auto messaging = XSE::GetMessagingInterface();
		messaging->RegisterListener(MessageHandler);
#endif 
		return true;
	}
};
