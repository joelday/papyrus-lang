# P5.04b — SFSE plugin v1

Appendix C v1 exit: attach via SFSE, breakpoint, stack, locals, original `.psc`. **No evaluate.**

This tree is the v1 skeleton. A full in-process DebugServer cannot be compiled here (macOS host, no MSVC / Starfield / Address Library dump). The Windows vcxproj, SFSE entry, and versioned JSON are landed so a later MSVC build can fill unpublished IDs without inventing them.

CommonLib: [libxse/commonlibsf](https://github.com/libxse/commonlibsf) overlay (`dependencies/ports/commonlibsf`). Runtime loader: [ianpatt/sfse](https://github.com/ianpatt/sfse). IDs: Address Library for SFSE Plugins (Nexus 3256), consumed through P5.02 JSON (`src/native/DebugServer/AddressLibraryIds.*`). Linking CommonLibSF is GPL-3.0-or-later with the Modding / Linking exceptions; the plugin source ships in this repo.

## What this PR implements

| Piece | Status |
|---|---|
| `DarkId.Papyrus.DebugServer.Starfield.vcxproj` | Landed. vcpkg feature `starfield` → CommonLibSF + xbyak. Shares `AddressLibraryIds`. |
| SFSE plugin entry | `SFSEPlugin_Version` (`UsesAddressLibrary`, `IsLayoutDependent`) + `SFSEPlugin_Load`. |
| Versioned JSON | `src/native/DebugServer/data/starfield-1.16.244.json` (Address Library v22 / game **1.16.244.0**). Unknown versions fail at load with a file list, not a later hook crash. |
| Locals IDs | **Published:** `GetVariable` `84986`, `GetPageForFrame` `138020`. Required to load. |
| Objects | **Published:** `Object_ctor` `137778`. Recorded; DAP object nodes are not wired in the stub. |
| Adjacent published IDs | GameVM singleton / bind, NativeFunctionBase Call/GetParam/GetSourceFilename/GetParamInfo, CodeTasklet VTABLE+RTTI, VM / CompiledScriptLoader / IVirtualMachine VTABLEs. See the JSON. |
| Launch via SFSE (#72) | Plugin metadata + `Data/SFSE/Plugins` copy target. |

## Blocked on unpublished Address Library IDs

Copied from `docs/debug/sfse-spike.md`. **Do not invent these IDs.** Locate them against 1.16.244.0 (or a newer versionlib) and add them to the versioned JSON.

| v1 feature | SE / FO4 hook | Starfield | Blocker |
|---|---|---|---|
| Instruction trampoline | `CodeTasklet::Process` | type + VTABLE `481606` only | **`CodeTaskletProcess` unpublished** |
| CreateStack | `Stack` ctor / VM create | `ID::BSScript::Stack::ctor` is `0` | **`CreateStack` unpublished** |
| CleanupStack | `Stack` dtor / VM cleanup | `ID::BSScript::Stack::dtor` is `0` | **`CleanupStack` unpublished** |
| Breakpoint at instruction offset | `GetInstructionNumberForOffset` + `ScriptFunction` | no `ScriptFunction` / `PackedInstructionStream` | **ID unpublished.** Fallback once trampolines exist: `IFunction::TranslateIPToLineNumber` (virtual) + Champollion `.pex` debug info. |
| Enable logging / trace / load-debug-info | INI pointer + `VM_ctor` call site | `CompiledScriptLoader::loadFlags` at `+0x32`; no pointer IDs | **Unsupported.** Workaround: `[Papyrus] bLoadDebugInformation=1` in `Starfield.ini`. |
| Background mouse / focus | SE-only | — | Out of scope v1 |
| `evaluate` / `setVariable` / hit-count | — | — | **P5.05b. Not implemented.** |
| DAP attach / stack frames / original `.psc` | existing DebugServer | not ported | Needs the trampolines above plus the SE/FO4 DAP port (`PapyrusDebugger`, `PexCache`, Champollion). `#define STACK_FRAME_IP ip` already matches Starfield. |

`GameScript::RemoteDebugger` exists in CommonLibSF; every function ID is `0`. v1 does **not** wrap it.

## Runtime behavior of the stub

1. SFSE loads `DarkId.Papyrus.DebugServer.Starfield.dll`.
2. `AddressLibraryIds` looks for `starfield-<runtime>.json` next to the DLL (`data/` then the plugin directory). `1.16.244.0` and `1-16-244-0` both match `starfield-1.16.244.json`.
3. Missing file → load returns false and SFSE unloads the plugin. Message lists the paths tried and any JSON that *is* present.
4. JSON missing `GetVariable` or `GetPageForFrame`, or either value is `0` → same hard fail. CommonLibSF uses `REL::ID{ 0 }` as the unpublished sentinel; a later dump that copies those zeros into the JSON must not be treated as hookable.
5. Published IDs are resolved. Missing or `0` trampoline IDs stay unpublished (not synthesized). The log prints the v1 matrix (`StarfieldV1::FormatStatus`) and leaves instruction/stack hooks uninstalled.

## Building (Windows / MSVC)

Same as the SE/FO4 plugins: VS 2022, vcpkg `integrate install`, build **this project only** (not "Build Solution") so vcpkg does not mix triplets. The Starfield project is in `DarkId.Papyrus.DebugServer.sln` but is not in the default solution build — CommonLibSF is not installed by CI yet. Right-click the Starfield project and Build.

Output: `src/DarkId.Papyrus.DebugServer/bin/DarkId.Papyrus.DebugServer.Starfield/x64/{Debug,Release}/` plus `data/starfield-*.json`. Install under `Data/SFSE/Plugins`.

This host (macOS) cannot produce the DLL. Portable pieces (`AddressLibraryIds`, `StarfieldV1`, the JSON) are verified with a host C++20 compile.

## Next increment (not this PR)

1. Dump / pattern-scan `CodeTasklet::Process`, `CreateStack`, `CleanupStack` on 1.16.244.0. Write the IDs into `starfield-1.16.244.json`.
2. Port `RuntimeEvents.cpp` trampolines and the DAP server. Use `GetVariable` (not `GetStackFrameVariable`) and `TranslateIPToLineNumber` if `GetInstructionNumberForOffset` is still missing.
3. Do not add evaluate here (P5.05b).
