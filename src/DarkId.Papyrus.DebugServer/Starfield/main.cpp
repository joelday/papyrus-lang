#include "pdsPCH.h"

#include "version.h"
#include "AddressLibraryIds.h"
#include "StarfieldV1.h"

#include <Windows.h>
#include <filesystem>
#include <vector>

using namespace DarkId::Papyrus::DebugServer;
using namespace std::literals;

static bool log_initialized = false;

static bool InitializeLog()
{
	if (log_initialized)
	{
		return true;
	}

	auto path = SFSE::log::log_directory();
	if (!path)
	{
		return false;
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

static bool LoadAddressLibraryOrFail(const SFSE::LoadInterface* a_sfse)
{
	const auto version = a_sfse->RuntimeVersion().string();
	const auto pluginDir = PluginDirectory();
	const std::vector<std::filesystem::path> search = {
		pluginDir / "data",
		pluginDir,
	};

	AddressLibraryIds::LoadError error;
	if (!AddressLibraryIds::Load(
			StarfieldV1::Game,
			version,
			StarfieldV1::Flavor,
			search,
			StarfieldV1::PublishedRequiredIds(),
			error))
	{
		logger::critical("{}", error.message);
		return false;
	}

	logger::info(
		"Loaded Address Library IDs for {} {} ({}) from {}"sv,
		AddressLibraryIds::LoadedGame(),
		AddressLibraryIds::LoadedVersion(),
		AddressLibraryIds::LoadedFlavor(),
		AddressLibraryIds::LoadedPath().string());
	return true;
}

static void LogV1Status()
{
	const auto status = StarfieldV1::Inspect();
	logger::info("{}", StarfieldV1::FormatStatus(status));

	// Do not invent trampoline IDs. Instruction / stack hooks stay uninstalled
	// until CodeTaskletProcess, CreateStack, and CleanupStack are published.
	if (!status.instructionTrampolineAvailable || !status.createStackAvailable || !status.cleanupStackAvailable)
	{
		logger::error(
			"SFSE v1 breakpoint/stack hooks are blocked on unpublished Address Library IDs. "
			"See docs/debug/sfse-plugin-v1.md");
	}
}

static void MessageHandler(SFSE::MessagingInterface::Message* msg)
{
	if (msg->type == SFSE::MessagingInterface::kPostDataLoad)
	{
		LogV1Status();
	}
}

SFSE_PLUGIN_VERSION = []() noexcept {
	SFSE::PluginVersionData v{};
	v.PluginVersion({ DIDPDS_VERSION_MAJOR, DIDPDS_VERSION_MINOR, DIDPDS_VERSION_PATCH, DIDPDS_VERSION_BUILD });
	v.PluginName("Papyrus Debug Server");
	v.AuthorName("Joel Day");
	v.UsesAddressLibrary(true);
	v.IsLayoutDependent(true);
	return v;
}();

SFSE_PLUGIN_LOAD(const SFSE::LoadInterface* a_sfse)
{
	SFSE::Init(a_sfse);
	if (!log_initialized)
	{
		InitializeLog();
	}

	logger::info("Papyrus Debug Server (Starfield / SFSE) loaded");

	if (!LoadAddressLibraryOrFail(a_sfse))
	{
		// Unknown or mismatched Starfield versions cannot be hooked safely.
		return false;
	}

	LogV1Status();

	if (auto* messaging = a_sfse->QueryInterface<SFSE::MessagingInterface>(SFSE::LoadInterface::kMessaging))
	{
		messaging->RegisterListener(MessageHandler);
	}
	else
	{
		logger::error("SFSE messaging interface is unavailable; plugin will not receive kPostDataLoad");
	}

	return true;
}
