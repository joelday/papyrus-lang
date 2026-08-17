#include "StarfieldV1.h"
#include "AddressLibraryIds.h"

#include <sstream>

namespace DarkId::Papyrus::DebugServer
{
	namespace
	{
		bool HasId(std::string_view name)
		{
			std::uint64_t unused = 0;
			return AddressLibraryIds::TryGet(name, unused);
		}

		void RequirePublished(StarfieldV1::Status& status, std::string_view name, bool& flag)
		{
			if (HasId(name))
			{
				flag = true;
				status.publishedIds.emplace_back(name);
				return;
			}
			flag = false;
			status.blocked.emplace_back(std::string(name) + " is not in the loaded Address Library JSON");
		}

		void NoteUnpublished(StarfieldV1::Status& status, std::string_view name, bool& flag, std::string_view why)
		{
			if (HasId(name))
			{
				flag = true;
				status.publishedIds.emplace_back(name);
				return;
			}
			flag = false;
			status.blocked.emplace_back(std::string(name) + " unpublished (" + std::string(why) + ")");
		}
	}

	const std::vector<std::string_view>& StarfieldV1::PublishedRequiredIds()
	{
		static const std::vector<std::string_view> ids = {
			"GetVariable",
			"GetPageForFrame",
		};
		return ids;
	}

	const std::vector<std::string_view>& StarfieldV1::PublishedOptionalIds()
	{
		static const std::vector<std::string_view> ids = {
			"Object_ctor",
			"GameVM_Singleton",
			"GameVM_BindEverythingToScript",
			"NativeFunctionBase_Call",
			"NativeFunctionBase_GetParam",
			"NativeFunctionBase_GetSourceFilename",
			"NativeFunctionBase_GetParamInfo",
			"VTABLE_CodeTasklet",
			"RTTI_CodeTasklet",
			"VTABLE_VirtualMachine",
			"VTABLE_CompiledScriptLoader",
			"VTABLE_IVirtualMachine",
		};
		return ids;
	}

	const std::vector<std::string_view>& StarfieldV1::UnpublishedTrampolineIds()
	{
		static const std::vector<std::string_view> ids = {
			"CodeTaskletProcess",
			"CreateStack",
			"CleanupStack",
			"GetInstructionNumberForOffset",
		};
		return ids;
	}

	const std::vector<std::string_view>& StarfieldV1::UnpublishedIniIds()
	{
		static const std::vector<std::string_view> ids = {
			"pPapyrusEnableLogging",
			"pPapyrusEnableTrace",
			"pPapyrusLoadDebugInformation",
			"VM_ctor",
		};
		return ids;
	}

	StarfieldV1::Status StarfieldV1::Inspect()
	{
		Status status;
		status.evaluateImplemented = false;
		status.addressLibraryLoaded = AddressLibraryIds::IsLoaded();
		if (!status.addressLibraryLoaded)
		{
			status.blocked.emplace_back(
				"Address Library JSON was not loaded; unknown Starfield versions cannot be hooked safely");
			return status;
		}

		RequirePublished(status, "GetVariable", status.localsIdsPresent);
		bool pageForFrame = false;
		RequirePublished(status, "GetPageForFrame", pageForFrame);
		status.localsIdsPresent = status.localsIdsPresent && pageForFrame;

		bool objectCtor = false;
		if (HasId("Object_ctor"))
		{
			objectCtor = true;
			status.publishedIds.emplace_back("Object_ctor");
		}
		status.objectCtorPresent = objectCtor;

		for (const auto name : PublishedOptionalIds())
		{
			if (name == "Object_ctor")
			{
				continue;
			}
			if (HasId(name))
			{
				status.publishedIds.emplace_back(name);
			}
		}

		NoteUnpublished(
			status,
			"CodeTaskletProcess",
			status.instructionTrampolineAvailable,
			"CommonLibSF has CodeTasklet type/VTABLE only; Process function ID is 0");
		NoteUnpublished(
			status,
			"CreateStack",
			status.createStackAvailable,
			"ID::BSScript::Stack::ctor is 0");
		NoteUnpublished(
			status,
			"CleanupStack",
			status.cleanupStackAvailable,
			"ID::BSScript::Stack::dtor is 0");
		NoteUnpublished(
			status,
			"GetInstructionNumberForOffset",
			status.getInstructionNumberForOffsetAvailable,
			"no ScriptFunction / PackedInstructionStream in CommonLibSF");

		bool anyIni = false;
		for (const auto name : UnpublishedIniIds())
		{
			bool present = false;
			NoteUnpublished(
				status,
				name,
				present,
				"no INI pointer or GameVM ctor call-site ID in CommonLibSF IDs.h");
			anyIni = anyIni || present;
		}
		status.papyrusIniHooksAvailable = anyIni;

		status.blocked.emplace_back("evaluate is P5.05b and is not implemented in SFSE v1");
		return status;
	}

	std::string StarfieldV1::FormatStatus(const Status& status)
	{
		std::ostringstream os;
		os << "Papyrus Debug Server SFSE v1 status\n";
		os << "  Address Library: " << (status.addressLibraryLoaded ? "loaded" : "missing") << '\n';
		if (status.addressLibraryLoaded)
		{
			os << "  game/version: " << AddressLibraryIds::LoadedGame() << ' '
			   << AddressLibraryIds::LoadedVersion() << " (" << AddressLibraryIds::LoadedFlavor() << ")\n";
			os << "  file: " << AddressLibraryIds::LoadedPath().string() << '\n';
		}
		os << "  locals (GetVariable + GetPageForFrame): "
		   << (status.localsIdsPresent ? "IDs present" : "unavailable") << '\n';
		os << "  Object::ctor: " << (status.objectCtorPresent ? "ID present" : "unavailable") << '\n';
		os << "  instruction trampoline (CodeTasklet::Process): "
		   << (status.instructionTrampolineAvailable ? "available" : "blocked") << '\n';
		os << "  CreateStack / CleanupStack: "
		   << (status.createStackAvailable && status.cleanupStackAvailable ? "available" : "blocked") << '\n';
		os << "  GetInstructionNumberForOffset: "
		   << (status.getInstructionNumberForOffsetAvailable ? "available" : "blocked") << '\n';
		os << "  Papyrus INI hooks: " << (status.papyrusIniHooksAvailable ? "available" : "unsupported")
		   << " (set [Papyrus] bLoadDebugInformation=1 in Starfield.ini)\n";
		os << "  evaluate: " << (status.evaluateImplemented ? "implemented" : "not in v1 (P5.05b)") << '\n';
		if (!status.publishedIds.empty())
		{
			os << "  published IDs loaded:";
			for (const auto& name : status.publishedIds)
			{
				os << ' ' << name;
			}
			os << '\n';
		}
		if (!status.blocked.empty())
		{
			os << "  blocked:\n";
			for (const auto& reason : status.blocked)
			{
				os << "    - " << reason << '\n';
			}
		}
		os << "  attach via SFSE: plugin entry is present. Breakpoint/stack hits require unpublished trampoline IDs.";
		return os.str();
	}
}
