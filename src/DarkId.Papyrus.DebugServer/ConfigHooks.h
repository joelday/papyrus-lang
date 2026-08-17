#pragma once
#include <xbyak/xbyak.h>
#include "GameInterfaces.h"
#include "AddressLibraryIds.h"
#include <REL/Relocation.h>
#include <string_view>

namespace DarkId::Papyrus::DebugServer
{
	namespace Game_Offset {
		inline REL::ID Id(std::string_view name)
		{
			return REL::ID(AddressLibraryIds::Get(name));
		}

		inline REL::ID pPapyrusEnableLogging() { return Id("pPapyrusEnableLogging"); }
		inline REL::ID pPapyrusEnableTrace() { return Id("pPapyrusEnableTrace"); }
		inline REL::ID pControlsBackgroundMouse() { return Id("pControlsBackgroundMouse"); }
		inline REL::ID pControlsBackgroundMouseDynamicInitializer() { return Id("pControlsBackgroundMouseDynamicInitializer"); }
		inline REL::ID GetInstructionNumberForOffset() { return Id("GetInstructionNumberForOffset"); }
		inline REL::ID VM_ctor() { return Id("VM_ctor"); }
		inline REL::ID CodeTaskletProcess() { return Id("CodeTaskletProcess"); }
		inline REL::ID CreateStack() { return Id("CreateStack"); }
		inline REL::ID CleanupStack() { return Id("CleanupStack"); }

#ifdef SKYRIM
		inline REL::ID pPapyrusLoadDebugInformation() { return Id("pPapyrusLoadDebugInformation"); }
		inline REL::ID InitWindows() { return Id("InitWindows"); }
		inline REL::ID bsrendererBegin() { return Id("bsrendererBegin"); }
		constexpr auto pControlsBackgroundMouseDynamicInitializerEndOffset = REL::VariantOffset(0x68, 0x65, 0x68); // the atexit call (TODO: Figure out the Skyrim VR offset)
		constexpr auto InitWindows_CreateWindowExA_Offset = REL::VariantOffset(0x163, 0x22C, 0x163);
		constexpr auto bsrendererBegin_GetClientRect_Offset = REL::VariantOffset(0x192, 0x18B, 0x192);
		constexpr auto ExpectedCreateWindowsExA_Offset = REL::VariantOffset(0x79779F, 0x84C34E, 0x000000);
#else // FALLOUT
		inline REL::ID pCurrentRenderWindow() { return Id("pCurrentRenderWindow"); }
		constexpr auto pControlsBackgroundMouseDynamicInitializerEndOffset = REL::Offset(0x59);
#endif
	}
	// Modified hook from PapyrusTweaks by NightFallStorm with permission
	struct EnableLoadDebugInformation
	{
#ifdef SKYRIM
		using ScriptLogger = RE::SkyrimScript::Logger;
#else
		using ScriptLogger = RE::GameScript::Logger;
#endif
		// Hook the <Skyrim/Game>VM's constructor that constructs CompiledScriptLoader, to force enable debug information loading
		// This thunk hook plays well with the doc string hook
		static RE::BSScript::CompiledScriptLoader* thunk(RE::BSScript::CompiledScriptLoader* a_unmadeSelf, ScriptLogger* a_unmadeLogger, bool a_loadDebugInformation, bool a_loadDocStrings)
		{
			return func(a_unmadeSelf, a_unmadeLogger, true, a_loadDocStrings);
		}

		static inline REL::Relocation<decltype(thunk)> func;
		
		// Hook target is SkyrimVM/GameVM's call to the CompiledScriptLoader constructor
		static inline REL::Relocation<std::uintptr_t> getHookTarget() {
#ifdef SKYRIM
			// Skyrim SE:
			// SkyrimVM::SkyrimVM():
			// - 1.5.97: offset at 0x0009204A0 (0x1409204A0)
			// - 1.6.640: offset at 0x00095F6E0 (0x14095F6E0)
			// Hooks at SkyrimVM() offset:
			// - 1.5.97: 0x604 (0x140920AA4)
			// - 1.6.640: 0x664 (0x14095FD44)
			// The hook target is the same address as asm instruction `call  <label_of_CompiledScriptLoader()>`
			return REL::Relocation<std::uintptr_t>(
				Game_Offset::VM_ctor(),
				REL::VariantOffset(0x604, 0x664, 0x604).offset());
#else
			// Fallout 4:
			// GameVM::GameVM(): offset at 0x001370D60 (0x141370D60)
			// Hooks at GameVM() offset 0x1C4 (0x141370F24)
			// The hook target is the same address as asm instruction `call <label_of_CompiledScriptLoader()>`
			return REL::Relocation<std::uintptr_t>(Game_Offset::VM_ctor(), REL::Offset(0x1C4).offset());
#endif

		}
		// Install our hook at the specified address
		static inline void Install()
		{
			REL::Relocation<std::uintptr_t> target = getHookTarget();
			stl::write_thunk_call<5, EnableLoadDebugInformation>(target.address());
			logger::info("EnableLoadDebugInformation hooked at address {:x}", target.address());
			logger::info("EnableLoadDebugInformation hooked at offset {:x}", target.offset());
		}
	};

	// We don't need this right now, just leaving it here
	struct overridemouseinitialize {
#if SKYRIM

		// hooks into RE::BSWin32MouseDevice::Initialize() by overwriting the vtable entry with this
		static inline void thunk(RE::BSWin32MouseDevice* _this) {
			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse().address();
			if (!*pBackgroundMouse) {
				*pBackgroundMouse = true;
			}
			return func(_this);
		}
		static inline REL::Relocation<decltype(thunk)> func;

		static inline void Install() {
			auto initializeVtableEntry = RE::BSWin32MouseDevice::VTABLE[0].address() + 8;
			REL::Relocation<std::uintptr_t> initializeLoc{ *(uintptr_t*)initializeVtableEntry };
			overridemouseinitialize::func = initializeLoc.address();
			REL::safe_write<std::uintptr_t>(initializeVtableEntry, stl::unrestricted_cast<std::uintptr_t>(overridemouseinitialize::thunk));
			logger::info("ForceEnableBackgroundMouse hooked at address {:x}", initializeLoc.address());
			logger::info("ForceEnableBackgroundMouse hooked at offset {:x}", initializeLoc.offset());
		}
#else
		static inline void Install() {}
#endif
	};

	// We don't need this right now, just leaving it here
	// This should be done before the INIs are loaded
	struct ForceEnableBackgroundMouse {
		// hooks into the dynamic SettingT<IniSettingCollection> initialzer for bBackgroundMouse and overrides the entry
		// we install into the call to `atexit`, so the thunk is thunking `int atexit(void(__cdecl*)())`
		static inline int thunk(void(__cdecl* atexitfunc)()) {
			auto result = func(atexitfunc);
			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse().address();
			*pBackgroundMouse = true;
			return result;
	    }
		static inline REL::Relocation<decltype(thunk)> func;

		static inline void Install() {
			// Set it here too in case it's not in the settings.ini
			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse().address();
			*pBackgroundMouse = true;
			REL::Relocation<std::uintptr_t> target(
				Game_Offset::pControlsBackgroundMouseDynamicInitializer().address() +
				Game_Offset::pControlsBackgroundMouseDynamicInitializerEndOffset.offset());
			stl::write_thunk_branch<5,ForceEnableBackgroundMouse>(target.address());
			logger::info("ForceEnableBackgroundMouse hooked at address {:x}", target.address());
			logger::info("ForceEnableBackgroundMouse hooked at offset {:x}", target.offset());
	    }
	};

	// In case we don't want to do the above
	// This should be done after the INIs have been loaded
	struct DyanmicallySetBackgroundMouse {
		static inline bool Set(bool enabled) {

			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse().address();
			if (*pBackgroundMouse != enabled) {
				*pBackgroundMouse = enabled;
#if SKYRIM
				auto devmanager = RE::BSInputDeviceManager::GetSingleton();
				if (devmanager) {
					auto mouse = devmanager->GetMouse();
					if (mouse) {
						if (mouse->dInputDevice && !mouse->notInitialized) {
							devmanager->ReinitializeMouse();
						}
						else {
							mouse->backgroundMouse = enabled;
						}
					}
				}
#endif	
				return true;
			}

			return false;
		}
	};
}