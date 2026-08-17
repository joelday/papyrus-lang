#pragma once

#include <cstdint>
#include <filesystem>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

namespace DarkId::Papyrus::DebugServer
{
	// Runtime Address Library IDs loaded from versioned JSON.
	// A missing file or unknown game version is a load error, not a later hook crash.
	class AddressLibraryIds
	{
	public:
		struct LoadError
		{
			std::string message;
		};

		// `flavor` selects a key inside `{ "se": ..., "ae": ... }` objects (`se`, `ae`, `vr`, `f4`).
		// Numeric JSON values are used as-is. Version strings like `1-6-640-0` and `1.6.640.0` both match
		// `skyrimse-1.6.640.json`.
		static bool Load(
			std::string_view game,
			std::string_view version,
			std::string_view flavor,
			const std::vector<std::filesystem::path>& searchDirectories,
			const std::vector<std::string_view>& requiredIds,
			LoadError& error);

		static bool Load(
			std::string_view game,
			std::string_view version,
			std::string_view flavor,
			const std::filesystem::path& dataDirectory,
			const std::vector<std::string_view>& requiredIds,
			LoadError& error);

		static void Reset() noexcept;
		static bool IsLoaded() noexcept;

		static const std::filesystem::path& LoadedPath();
		static const std::string& LoadedGame();
		static const std::string& LoadedVersion();
		static const std::string& LoadedFlavor();

		static bool TryGet(std::string_view name, std::uint64_t& out) noexcept;
		// Throws std::runtime_error with a clear message if not loaded or the name is absent.
		static std::uint64_t Get(std::string_view name);

		static const std::unordered_map<std::string, std::uint64_t>& All();
		static std::vector<std::string> AvailableFiles(const std::filesystem::path& dataDirectory);
	};
}
