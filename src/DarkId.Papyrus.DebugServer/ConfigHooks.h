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

#else // FALLOUT
		// Where the bEnableLogging fallout4.ini setting is stored; this is overwritten when the ini is loaded
		constexpr auto pPapyrusEnableLogging = REL::ID(1272228); // 14380E140
		// Where the bEnableTrace fallout4.ini setting is stored; overwritten on ini load
		constexpr auto pPapyrusEnableTrace = REL::ID(218028); // 143818DC0
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
			stl::write_thunk_call<EnableLoadDebugInformation>(target.address());
			logger::info("EnableLoadDebugInformation hooked at address {:x}", target.address());
			logger::info("EnableLoadDebugInformation hooked at offset {:x}", target.offset());
		}

	};
}