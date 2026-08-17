#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace DarkId::Papyrus::DebugServer
{
	// Appendix C v1 surface for Starfield. Only published Address Library IDs are
	// treated as usable. Unpublished trampoline IDs are never invented.
	class StarfieldV1
	{
	public:
		static constexpr std::string_view Game = "starfield";
		static constexpr std::string_view Flavor = "sf";

		struct Status
		{
			bool addressLibraryLoaded = false;
			bool localsIdsPresent = false;
			bool objectCtorPresent = false;
			bool instructionTrampolineAvailable = false;
			bool createStackAvailable = false;
			bool cleanupStackAvailable = false;
			bool getInstructionNumberForOffsetAvailable = false;
			bool papyrusIniHooksAvailable = false;
			bool evaluateImplemented = false;
			std::vector<std::string> publishedIds;
			std::vector<std::string> blocked;
		};

		// IDs the JSON must contain for a known Starfield version.
		static const std::vector<std::string_view>& PublishedRequiredIds();
		// Recorded from CommonLibSF; useful but not required to load.
		static const std::vector<std::string_view>& PublishedOptionalIds();
		// Required for SE/FO4-style trampolines. Absent from CommonLibSF IDs.h.
		static const std::vector<std::string_view>& UnpublishedTrampolineIds();
		// INI-pointer hooks. Absent; Starfield.ini is the workaround.
		static const std::vector<std::string_view>& UnpublishedIniIds();

		static Status Inspect();
		static std::string FormatStatus(const Status& status);
	};
}
