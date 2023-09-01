#pragma once
#include <xbyak/xbyak.h>
#include "GameInterfaces.h"
#include <REL/Relocation.h>

namespace DarkId::Papyrus::DebugServer
{
	namespace Game_Offset {
#ifdef SKYRIM
		// Where the bEnableLogging skyrim.ini setting is stored; this is overwritten when the ini is loaded
		constexpr auto pPapyrusEnableLogging = RELOCATION_ID(510627, 383715);        // 1.5.97: 141DF5AE8,   1.6.640: 141E88DC8
		constexpr auto pPapyrusEnableTrace = RELOCATION_ID(510667, 383766);          // 1.5.97: 141DF5C60,   1.6.640: 141E88F88
		constexpr auto pPapyrusLoadDebugInformation = RELOCATION_ID(510650, 383743); // 1.5.97: 141DF5BC0,   1.6.640: 141E88EA0
		constexpr auto pControlsBackgroundMouse = RELOCATION_ID(511920, 388493);     // pcontrols background mouse
		constexpr auto pControlsBackgroundMouseDynamicInitializer = RELOCATION_ID(9099, 9144);  // 1.5.97: 1400D9320, 1.6.640: 1400E1020
		constexpr auto pControlsBackgroundMouseDynamicInitializerEndOffset = REL::VariantOffset(0x68, 0x65, 0x68); // the atexit call (TODO: Figure out the Skyrim VR offset)
		constexpr auto InitWindows = RELOCATION_ID(75591, 77226);
		constexpr auto InitWindows_CreateWindowExA_Offset = REL::VariantOffset(0x163, 0x22C, 0x163);
		constexpr auto bsrendererBegin = RELOCATION_ID(75460, 77245);
		constexpr auto bsrendererBegin_GetClientRect_Offset = REL::VariantOffset(0x192, 0x18B, 0x192);
		constexpr auto ExpectedCreateWindowsExA_Offset = REL::VariantOffset(0x79779F, 0x84C34E, 0x000000);

		// BSScript__ByteCode__PackedInstructionStream__GetInstructionNumberForOffset
		constexpr auto GetInstructionNumberForOffset = RELOCATION_ID(97807, 104551); // 1.5.97: 1248FA0, 1.6.640: 141371400
#else // FALLOUT
		// Where the bEnableLogging fallout4.ini setting is stored; this is overwritten when the ini is loaded
		constexpr auto pPapyrusEnableLogging = REL::ID(1272228); // 14380E140
		// Where the bEnableTrace fallout4.ini setting is stored; overwritten on ini load
		constexpr auto pPapyrusEnableTrace = REL::ID(218028); // 143818DC0
		constexpr auto pControlsBackgroundMouse = REL::ID(187076); //143846670
		constexpr auto pControlsBackgroundMouseDynamicInitializer = REL::ID(267843); //142AEDB40
		constexpr auto pControlsBackgroundMouseDynamicInitializerEndOffset = REL::Offset(0x59);
		// BSScript__ByteCode__PackedInstructionStream__GetInstructionNumberForOffset

		constexpr auto GetInstructionNumberForOffset = REL::ID(1126425); //14278A1B0

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
			return REL::Relocation<std::uintptr_t>(RELOCATION_ID(53108, 53919), REL::VariantOffset(0x604, 0x664, 0x604));
#else
			// Fallout 4:
			// GameVM::GameVM(): offset at 0x001370D60 (0x141370D60)
			// Hooks at GameVM() offset 0x1C4 (0x141370F24)
			// The hook target is the same address as asm instruction `call <label_of_CompiledScriptLoader()>`
			return REL::Relocation<std::uintptr_t>(REL::ID(35833), REL::Offset(0x1C4).offset());
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
			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse.address();
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
			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse.address();
			*pBackgroundMouse = true;
			return result;
	    }
		static inline REL::Relocation<decltype(thunk)> func;

		static inline void Install() {
			// Set it here too in case it's not in the settings.ini
			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse.address();
			*pBackgroundMouse = true;
			REL::Relocation<std::uintptr_t> target(
				Game_Offset::pControlsBackgroundMouseDynamicInitializer.address() +
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

			bool* pBackgroundMouse = (bool*)Game_Offset::pControlsBackgroundMouse.address();
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