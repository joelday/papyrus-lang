# P5.04a — CommonLibSF spike (Appendix C hook inventory)

This is a locate-and-document spike. It does **not** implement the Starfield DebugServer (that is P5.04b).

## Pins

| Piece | Value |
|---|---|
| CommonLib repo | [libxse/commonlibsf](https://github.com/libxse/commonlibsf) |
| CommonLib commit | `6eeb9e60590db21b6f983d487b6b2d51a0fb6726` (`main`, 2026-07-24, "feat: merge pr #46 from Meridiano/patch-1") |
| `lib/commonlib-shared` gitlink | [libxse/commonlib-shared](https://github.com/libxse/commonlib-shared) `@ 5470284e964d5510aa001dca3e0bb5548b6356a4` (as recorded on that CommonLibSF commit) |
| Overlay port | `dependencies/ports/commonlibsf` (`version-date` `2026-07-24`) |
| Runtime loader | [ianpatt/sfse](https://github.com/ianpatt/sfse) (still required; CommonLibSF replaces SFSE only as a *static* dependency) |
| Address Library | [Nexus Starfield 3256](https://www.nexusmods.com/starfield/mods/3256) — current public file at writing is **v22 / game 1.16.244.0**. IDs are consumed at runtime via `REL::IDDB` (`Loader::SFSE`). |

### Why this pin (and not the CMake fork)

- Appendix C names **libxse/commonlibsf**, the maintained successor of `Starfield-Reverse-Engineering/CommonLibSF` (that repo's README now redirects here).
- The official [CommonLibSF-vcpkg](https://github.com/Starfield-Reverse-Engineering/CommonLibSF-vcpkg) registry still fetches `Starfield-Reverse-Engineering/CommonLibSF` `@ 0f8989ab…` and runs CMake. That snapshot predates the **1.15.216 Address Library remap** (meh321, 2025-05-24: *1.14.x and lower IDs no longer correspond to 1.15.x+*). Pinning it would give us IDs that do not resolve against current `versionlib` databases.
- libxse dropped CMake for **xmake 3** plus the `commonlib-shared` submodule (REL/REX). The overlay therefore vendors the recorded submodule commit and injects a CMake wrapper so this repo's vcpkg/MSBuild pipeline can `find_package(CommonLibSF)`.
- License is **GPL-3.0-or-later WITH Modding Exception AND GPL-3.0 Linking Exception** (`COPYING` + `EXCEPTIONS`). SFSE is a named Modding Library. P5.04b must ship source if it links CommonLibSF.

IDs below are copied from those two commits. **IDs that are `0` or absent are unpublished — they are not invented here.**

## Object model (exists in CommonLibSF)

The SE/FO4 names mostly survive. Layouts differ; P5.04b must not assume SE/FO4 offsets.

| Type | Header | Size / notes |
|---|---|---|
| `RE::BSScript::Internal::CodeTasklet` | `include/RE/C/CodeTasklet.h` | `0x50`. Members: `stack` `+0x10`, `vm` `+0x18`, `topFrame` `+0x38`, `instructionDataBitCount` `+0x44`. Same `OpCode` enum as FO4 plus Starfield `kLOCK_GUARDS` / `kUNLOCK_GUARDS` / `kTRY_LOCK_GUARDS`. |
| `RE::BSScript::Stack` | `include/RE/S/Stack.h` | `0xC8`. `top` `+0x68`, `owningTasklet` `+0x70`, `stackID` `+0xB4`, `state` `+0xB8`, `freezeState` `+0xBC`. New states: `kWaitingOnGuard`, `kWaitingOnOtherStackForGuard`. `unk10` is noted as guard-related. |
| `RE::BSScript::StackFrame` | `include/RE/S/StackFrame.h` | `0x40`. IP is `ip` at `+0x34` (FO4 already uses `ip`; SE uses `instructionPointer` — `pdsPCH.h` `#define STACK_FRAME_IP`). Also `owningFunction`, `owningObjectType`, `self`, `previousFrame`. |
| `RE::BSScript::IFunction` | `include/RE/I/IFunction.h` | Virtuals used by today's locals/breakpoints: `GetStackFrameSize`, `GetIsStatic`, `GetIsNative`, `GetVarNameForStackIndex`, `GetSourceFilename`, `TranslateIPToLineNumber`. Two new unknown virtuals (`Unk_12` / `Unk_13`). |
| `RE::BSScript::Internal::VirtualMachine` | `include/RE/V/VirtualMachine.h` | `0x4040`. `vmTasks` (`BSTArray<CodeTasklet*>` `+0x740`), `allRunningStacks` hashmap `+0x3CD0`, `GetStackByID` (save-load virtual `0x16`), `RegisterForLogEvent` (IVirtualMachine virtual `0x3D`), `CreateEmptyTasklet` (`0x1F`). Singleton via `GameVM`. |
| `RE::GameVM` | `include/RE/G/GameVM.h` | `scriptLoader` (`CompiledScriptLoader` `+0x160`), `logger` (`GameScript::Logger` `+0x1E8`), `remoteDebugger` `+0x7B8`. |
| `RE::BSScript::CompiledScriptLoader` | `include/RE/C/CompiledScriptLoader.h` | `0x38`. Debug-info load is a `loadFlags` byte at `+0x32`, not the SE/FO4 ctor `bool a_loadDebugInformation`. |
| `RE::BSScript::Variable` / `Array` / `Object` / `Struct` | `include/RE/{V,A,O,S}/` | Present. `TypeInfo::RawType` has object/string/int/float/bool/var/struct and matching arrays. **No `kGuard` raw type.** |
| `RE::BSScript::ObjectTypeInfo` | `include/RE/O/ObjectTypeInfo.h` | `CountData` is mostly unnamed. Header comment: *this likely has … `guardCount` … but it's not clear which is which.* |

**Missing types relative to today's plugin:** there is **no** `RE::BSScript::Internal::ScriptFunction` and **no** `RE::BSScript::ByteCode::PackedInstructionStream`. `BreakpointManager.cpp` currently `static_cast`s to `ScriptFunction*` and calls `GetInstructionNumberForOffset(&func->instructions, ip)`. Those symbols are not in this pin.

## Appendix C hook inventory

SE/FO4 IDs are the ones already hardcoded in `RuntimeEvents.cpp` / `ConfigHooks.h`. SF IDs are only listed when they appear as a non-zero `REL::ID` in CommonLibSF.

| Feature (Appendix C) | SE | FO4 | SF analogue in CommonLibSF? | Public SF Address Library ID | v1 |
|---|---|---|---|---|---|
| Instruction-execution trampoline (`CodeTasklet::Process`) | `RELOCATION_ID(98520, 105176)` + cave `0xD6`/`0xCA` | `REL::ID(614585)` + `0xFD` | **Type yes; `Process` function ID no.** VTABLE `REL::ID(481606)`, RTTI `REL::ID(870890)`. | **Not published** | **Required.** P5.04b must locate `Process` (or the instruction loop) by dump / pattern; do not invent an ID here. |
| CreateStack | `RELOCATION_ID(98146, 104870)` + `0x1D4`/`0x1D9` | `REL::ID(107558)` | `Stack` type yes. `ID::BSScript::Stack::ctor` is **`0`** (comment-only old ID `196325`). VM exposes `allRunningStacks` + `GetStackByID`. | **Not published** | **Required.** |
| CleanupStack | `RELOCATION_ID(98149, 104873)` | `REL::ID(38511)` | `ID::BSScript::Stack::dtor` is **`0`** (comment-only `196333`). | **Not published** | **Required.** |
| Log event sink | `VirtualMachine::RegisterForLogEvent` | same | Same virtual (`IVirtualMachine` `0x3D`). `ErrorLogger` is `BSTEventSource<LogEvent>`. `GameScript::Logger` subclasses it. | No function ID needed (vtable) | Required |
| Breakpoint at instruction offset | `GetInstructionNumberForOffset` = `RELOCATION_ID(97807, 104551)` + `ScriptFunction::instructions` | `REL::ID(1126425)` | **No `ScriptFunction`, no `PackedInstructionStream`, no `GetInstructionNumberForOffset`.** Fallback that *is* public: `IFunction::TranslateIPToLineNumber` (virtual). | **Not published** | **Required.** Either reverse `ScriptFunction` in P5.04b or map `StackFrame::ip` via `TranslateIPToLineNumber` + Champollion `.pex` debug info. |
| Enable papyrus logging / trace / load-debug-info INI | `RELOCATION_ID(510627/510667/510650, 383715/383766/383743)` | `REL::ID(1272228)` / `REL::ID(218028)` | `CompiledScriptLoader::loadFlags`. No INI *pointer* IDs in `IDs.h`. GameVM ctor / `CompiledScriptLoader` call-site ID (FO4 `REL::ID(35833)+0x1C4`) is **absent**. | **Not published** | Appendix C: *v1 if IDs exist; else document as unsupported.* **Unsupported until IDs are found.** Starfield.ini still has `[Papyrus] bLoadDebugInformation` as a player-side workaround. |
| Background-mouse / focus | SE `RELOCATION_ID(511920, 388493)` etc. | n/a | Not searched beyond Appendix C. | — | **Out of scope v1** |
| Locals | `StackFrame::GetStackFrameVariable` | same | **`StackFrame::GetVariable`** wraps `ID::BSScript::StackFrame::GetVariable`. Page hint: `Stack::GetPageForFrame`. `GetStackFrameVariable` is **`0` / inlined**. | **`84986`** (`GetVariable`); **`138020`** (`GetPageForFrame`) | **Required** (name change only) |
| Objects / arrays / structs in DAP | yes | yes | Types exist (`Array`, `Object`, `Struct`, `StructTypeInfo`). `Object::ctor` = `137778`. | `137778` (ctor only) | **Required** |
| Guards as watchable state | n/a | n/a | Opcodes + `Stack::State::kWaitingOnGuard` + `VirtualMachine::stacksWaitingOnGuard` + `GuardDataIFace`. Not a `TypeInfo` kind. `ObjectTypeInfo` guard slots unmapped. | **None** | **Best-effort v1** (Appendix C) |
| `evaluate` interpreter | — | — | Shared C# adapter (P5.05). Plugin still needs get/set of the values above. | — | **Not required for SFSE v1** |
| Launch via SFSE | — | — | `SFSE::PluginVersionData` (`UsesAddressLibrary`, `IsLayoutDependent`). | — | **Required** (#72) |

### Other published IDs adjacent to the debugger (not Appendix C hooks)

Recorded so P5.04b does not have to rediscover them. Still only values from `include/RE/IDs.h` / `IDs_VTABLE.h`.

| Symbol | `REL::ID` |
|---|---|
| `ID::GameVM::Singleton` | `937585` |
| `ID::GameVM::BindEverythingToScript` | `117899` |
| `ID::BSScript::Internal::NF_util::NativeFunctionBase::Call` | `138074` |
| `ID::BSScript::Internal::NF_util::NativeFunctionBase::GetParam` | `138061` |
| `ID::BSScript::Internal::NF_util::NativeFunctionBase::GetSourceFilename` | `138065` |
| `ID::BSScript::Internal::NF_util::NativeFunctionBase::GetParamInfo` | `138067` |
| `VTABLE::BSScript__Internal__VirtualMachine` | `481117` (plus `481114`, `481112`, `481129`, `481127`, `481125`, `481123`) |
| `VTABLE::BSScript__CompiledScriptLoader` | `480919` |
| `VTABLE::BSScript__IVirtualMachine` | `480577` |

`IDs.h` keeps **pre-1.15 IDs in comments** (e.g. `GetPageForFrame{ 138020 }; // 196366`). Those commented numbers are historical. After 1.15.216 they do not resolve.

### Bethesda `GameScript::RemoteDebugger`

CommonLibSF already models an in-engine DAP-like debugger (`include/RE/R/RemoteDebugger.h`, `DebuggerMessages.h`): continue / pause / disconnect / set-breakpoints / stack-trace / threads / variables / value / step. Every `ID::GameScript::RemoteDebugger::*` entry is **`0`** (comments only: `167579`–`167587`). We do **not** wrap this for v1; the product debugger remains our plugin, same as SE/FO4.

## What is out of scope for v1

From Appendix C plus what this spike will not do:

- The full DebugServer Starfield port (P5.04b). No `DarkId.Papyrus.DebugServer.Starfield.vcxproj`, no SFSE plugin binary.
- Inventing Address Library IDs for `CodeTasklet::Process`, `CreateStack`, `CleanupStack`, `GetInstructionNumberForOffset`, or Papyrus INI pointers.
- Enabling papyrus logging / trace / load-debug-info via INI-pointer hooks (IDs not public).
- Background-mouse / focus hacks.
- Conditional / hit-count breakpoints and the interpreted `evaluate` / `setVariable` (P5.05).
- Wrapping `GameScript::RemoteDebugger`.
- Consuming P5.02 versioned JSON (that PR does not exist yet). P5.04b must switch to it once it does.
- Building CommonLibSF in CI on this spike (port is `windows & x64`; this tree is checked in from macOS).

## P5.04b implications (not implemented here)

1. **Three hook IDs are still unknown:** `CodeTasklet::Process`, `CreateStack`, `CleanupStack`. Those are the SE/FO4 trampoline targets in `RuntimeEvents.cpp`. Locating them is the first P5.04b task (IDA / Address Library dump against 1.16.244.0, then record in the P5.02 JSON — do not hardcode a guessed ID).
2. **Locals can be ported from public IDs.** Use `StackFrame::GetVariable` (`84986`) and `Stack::GetPageForFrame` (`138020`). Rename `GetStackFrameVariable`.
3. **Breakpoints need a new path.** Either recover `ScriptFunction` + `PackedInstructionStream` + `GetInstructionNumberForOffset`, or drive line hits from `IFunction::TranslateIPToLineNumber` plus the Champollion `.pex` we already load.
4. **IP field name is already abstracted:** `#define STACK_FRAME_IP ip` is the FO4 spelling and matches Starfield.
5. **xbyak is required** for the same cave/trampoline style. The overlay exposes feature `commonlibsf[xbyak]`; root `vcpkg.json` feature `starfield` requests it.
6. **C++23 / xmake-era REL.** P5.04b cannot `#include <SKSE/…>` or `<F4SE/…>`. Entry point is `SFSEPlugin_Version` + `SFSE::Init`. Address Library is `UsesAddressLibrary(true)` (v2 bit).
7. **GPL-with-exception.** Linking CommonLibSF obliges shipping corresponding source for the plugin.

## v1 exit (unchanged from Appendix C)

Attach to a Starfield session started through SFSE; set a breakpoint in a vanilla script whose source is on disk; hit it; see a stack, locals, and the **original** `.psc` (#180). No evaluate, no conditional breakpoints, no mouse hooks.
