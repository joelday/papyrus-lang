#define XBYAK_NO_OP_NAMES

#include "RuntimeEvents.h"

#include <xbyak/xbyak.h>

#if SKYRIM
#include <SKSE/Events.h>
#include <RE/B/BSTEvent.h>
#include <REL/Relocation.h>
#elif FALLOUT
#include <REL/Relocation.h>
#include <F4SE/Trampoline.h>
#endif

#include <cassert>
#include <mutex>
#include <dap/protocol.h>

namespace DarkId::Papyrus::DebugServer
{
	namespace RuntimeEvents
	{
#define EVENT_WRAPPER_IMPL(NAME, HANDLER_SIGNATURE) \
		eventpp::CallbackList<HANDLER_SIGNATURE> g_##NAME##Event; \
		\
		NAME##EventHandle SubscribeTo##NAME(std::function<HANDLER_SIGNATURE> handler) \
		{ \
			return g_##NAME##Event.append(handler); \
		} \
		\
		bool UnsubscribeFrom##NAME(NAME##EventHandle handle) \
		{ \
			return g_##NAME##Event.remove(handle); \
		} \

		EVENT_WRAPPER_IMPL(InstructionExecution, void(RE::BSScript::Internal::CodeTasklet*))
		EVENT_WRAPPER_IMPL(CreateStack, void(RE::BSTSmartPointer<RE::BSScript::Stack>&))
		EVENT_WRAPPER_IMPL(CleanupStack, void(uint32_t))
		// EVENT_WRAPPER_IMPL(InitScript, void(RE::TESInitScriptEvent*))
		EVENT_WRAPPER_IMPL(Log, void(const RE::BSScript::LogEvent*))
		EVENT_WRAPPER_IMPL(BreakpointChanged, void(const dap::Breakpoint& bpoint, const std::string&))
#undef EVENT_WRAPPER_IMPL
		
		//class ScriptInitEventSink : public RE::BSTEventSink<RE::TESInitScriptEvent>
		//{
		//	RE::EventResult ReceiveEvent(RE::TESInitScriptEvent* evn, RE::BSTEventSource<RE::TESInitScriptEvent>* a_eventSource) override
		//	{
		//		g_InitScriptEvent(evn);

		//		return RE::EventResult::kContinue;
		//	};
		//};

		class LogEventSink : public RE::BSTEventSink<RE::BSScript::LogEvent>
		{
			using EventResult = RE::BSEventNotifyControl;
			#ifdef SKYRIM
			EventResult ProcessEvent(const RE::BSScript::LogEvent* a_event, RE::BSTEventSource<RE::BSScript::LogEvent>*) override
			{
				g_LogEvent(a_event);
				return RE::BSEventNotifyControl::kContinue;
			};
			#else // FALLOUT
			EventResult ProcessEvent(const RE::BSScript::LogEvent& a_event, RE::BSTEventSource<RE::BSScript::LogEvent>*) override
			{
				g_LogEvent(&a_event);
				return RE::BSEventNotifyControl::kContinue;
			};
			#endif

		};
		struct CallPatch : Xbyak::CodeGenerator
		{
		protected:
			void saveVolatiles() {
				push(rax);	// save volatile registers
				push(rcx);
				push(rdx);
				push(r8);
				push(r9);
				push(r10);
				push(r11);
				push(r11);
			}
			void loadVolatiles() {
				pop(r11); // load the saved volatile registers
				pop(r11);
				pop(r10);
				pop(r9);
				pop(r8);
				pop(rdx);
				pop(rcx);
				pop(rax);
			}
		};

#if SKYRIM
		struct UnknownInstructionData {
			uint32_t unk00;
			uint32_t pad04;
			uint32_t unk08;
			uint32_t pad0C;
		};
		static_assert(sizeof(UnknownInstructionData) == 0x10);

		void InstructionExecute_Hook(RE::BSScript::Internal::CodeTasklet* a_tasklet, uint32_t currentIP)
		{
			if (a_tasklet->topFrame)
			{
				// assign the correct IP
				a_tasklet->topFrame->STACK_FRAME_IP = currentIP;
				g_InstructionExecutionEvent(a_tasklet);
			}
		}

		void CreateStack_Hook(RE::BSTSmartPointer<RE::BSScript::Stack>& a_stack)
		{
			if (&a_stack)
			{
				g_CreateStackEvent(a_stack);
			}
		}

		using CleanupStack_t = void(RE::BSScript::Internal::VirtualMachine*, RE::BSScript::Stack*);
		CleanupStack_t* _CleanupStack = 0;

		void CleanupStack_Hook(RE::BSScript::Internal::VirtualMachine* a_vm, RE::BSScript::Stack* a_stack)
		{
			if (a_stack) {
				const auto stackID = a_stack->stackID;
				_CleanupStack(a_vm, a_stack);

				g_CleanupStackEvent(stackID);
			}
		}

		namespace Internal
     	{
			struct SetIPPatch {

				struct Patch : CallPatch
				{

					Patch(std::uintptr_t a_retAddr, std::uintptr_t a_ifFreezeLabelAddr)
					{
						Xbyak::Label retLbl;
						Xbyak::Label isFrozen;
						Xbyak::Label ifFreezeLabel;
						Xbyak::Label ifgeInstructionDataBitCountLabel;
						/**
						* The main issue we are trying to solve is that the InstructionPointer on the top stack frame
						* doesn't get updated until the tasklet actually finishes executing. We need this to be set in order for our subsequent call to work
						*
						* It doesn't do this until it either:
						* - reaches the end of the instruction bitstream
						* - the stack is about to freeze
						* - the max ops per tasklet have been executed (100)
						*
						* The actual IP is set in edx before checking for the first two conditions, and this will be used to set the topFrame's IP if the tasklet exits
						* So we need to install our branch right after that to be able to set the IP with the correct value
						* We install into the `jz` instruction since it's a 6-byte long jump.
						*
						* Here's our hook target, near the start of the main loop:
						* ```
						*  lea     edx, [rax+rcx*8]								      # at this point, edx holds the actual current IP
						*  cmp     dword ptr [rax+6Ch], 1                             # check if this->stack->freeze state is 1 (frozen)
						*  jz      if_frozen_label                                    # jumps if above comparison is true               <-- branch installed here
						*  cmp     edx, [<rsi/rdi>+40h]                               # compare the current IP to this->InstructionDataBitCount ("(CodeTasklet) this" is rsi in AE, rdi in SE)
						*  jb      short if_less_than_InstructionDataBitCount_label   # jump if the current IP is less than this->InstructionDataBitCount
						* ```
						*
						* Since we hook right in the middle of the checking of the first two conditions, we want to check those before attempting to set the IP.
						*  - for stack freeze, it's going to be assigned anyway
						*  - if we set the IP to anything >= InstructionDataBitCount it will mess up the return
						* 
						* the current ops count and max ops comparison happens at the end of the loop, so we don't have to check that
						*/
						cmp(dword[rax + 0x6C], 1); // check to see if stack->freeze state is 1 (frozen)
						jz(isFrozen); // we overwrite this instruction, so we have to jump to our saved address
						if (REL::Module::IsAE()) {
							cmp(edx, dword[rsi + 0x40]); // (CodeTasklet)this is rsi in AE
						}
						else {
							cmp(edx, dword[rdi + 0x40]); // (CodeTasklet)this is rdi in SE
						}
						// originally a `jb` that skips the main switch case; we just want this to return if the above comparison is true
						// we didn't overwrite that jump or the above comparison, so we can just return to the return address
						jge(ifgeInstructionDataBitCountLabel);

						mov(dword[rax + 0x20], edx); // set the instruction pointer to the current IP

						L(ifgeInstructionDataBitCountLabel);
						jmp(ptr[rip + retLbl]);	// resume execution

						L(retLbl);
						dq(a_retAddr);

						L(isFrozen);
						jmp(ptr[rip + ifFreezeLabel]);

						L(ifFreezeLabel);
						dq(a_ifFreezeLabelAddr);

					}

				};
				static inline void Install()
				{
					// InstructionExecute
					// 1.5.97:  0x141278110: BSScript__Internal__CodeTasklet::VMProcess_141278110
					// 1.6.640: 0x14139C860: BSScript__Internal__CodeTasklet::sub_14139C860
					// 1_5_97   CAVE_START = 0x170
					// 1_6_640  CAVE_START = 0x14C
					// 1_5_97   CAVE_END   = 0x176
					// 1_6_640  CAVE_END   = 0x153
					// Cave start and cave end indicate the beginning and end of the instructions 
					// We install near the beginning of the loop
					// The installation target is the `jz` instruction that jumps if the freeze state is 1 
					// CAVE_SIZE = 6 
					auto vmprocess_reloc = RELOCATION_ID(98520, 105176);

					//TODO: Find VR offsets, using SE offsets as placeholders
					auto cave_start_var_offset = REL::VariantOffset(0xD6, 0xCA, 0xD6);
					auto cave_end_var_offset = REL::Offset(cave_start_var_offset.offset() + 6);

					REL::Relocation<std::uintptr_t> cave_start_reloc{ vmprocess_reloc, cave_start_var_offset };
					REL::Relocation<std::uintptr_t> cave_end_reloc{ vmprocess_reloc, cave_end_var_offset };
					std::size_t CAVE_START = cave_start_var_offset.offset();
					std::size_t CAVE_END = cave_end_var_offset.offset();
					std::size_t CAVE_SIZE = CAVE_END - CAVE_START;

					assert(CAVE_SIZE >= 6);
					// we need to read what the offset is in the `jz` instruction;
					// jz instruction is opcode (`0F 84`) followed by a four-byte offset
					auto if_freeze_label_offset_loc_offset = REL::Offset(cave_start_var_offset.offset() + 2);
					REL::Relocation<std::uintptr_t> if_freeze_label_offset_loc_addr{ vmprocess_reloc, if_freeze_label_offset_loc_offset };
					uint32_t* ptr_to_offset = (uint32_t*)if_freeze_label_offset_loc_addr.address();
					uint32_t offset_val = *ptr_to_offset;
					auto if_freeze_label_offset = REL::Offset(offset_val + cave_end_var_offset.offset()); // the offset relative to the address AFTER the jz instruction
					REL::Relocation<std::uintptr_t> if_freeze_label_address{ vmprocess_reloc, if_freeze_label_offset };

					auto patch = Patch(cave_end_reloc.address(), if_freeze_label_address.address());
					auto& trampoline = SKSE::GetTrampoline();
					SKSE::AllocTrampoline(patch.getSize() + 14);
					auto result = trampoline.allocate(patch);
					trampoline.write_branch<6>(cave_start_reloc.address(), (std::uintptr_t)result);
					auto BASE_LOAD_ADDR = vmprocess_reloc.address() - vmprocess_reloc.offset();
					logger::info("Base for executable is: 0x{:X}", BASE_LOAD_ADDR);
					logger::info("CodeTasklet::Process address: 0x{:X}", vmprocess_reloc.address());
					logger::info("CodeTasklet::Process relocation offset: 0x{:X}", vmprocess_reloc.offset());

					logger::info("SetIPPatch installed at address 0x{:X}", cave_start_reloc.address());
					logger::info("SetIPPatch installed at offset 0x{:X}", cave_start_reloc.offset());
					logger::info("SetIPPatch if_freeze_label_offset at address 0x{:X}", if_freeze_label_offset.address());
					logger::info("SetIPPatch if_freeze_label_offset at offset 0x{:X}", if_freeze_label_offset.offset());
					logger::info("SetIPPatch:CAVE_START is 0x{:X}", CAVE_START);
					logger::info("SetIPPatch:CAVE_END is 0x{:X}", CAVE_END);
					logger::info("SetIPPatch:CAVE_SIZE is 0x{:X}", CAVE_SIZE);

					std::size_t RESULT_ADDR = (std::uintptr_t)result;
					logger::info("SetIPPatch patch allocation address: 0x{:X}", RESULT_ADDR);
					logger::info("SetIPPatch patch allocation offset: 0x{:X}", RESULT_ADDR - BASE_LOAD_ADDR);

				}
			};
			struct InstructionExecuteHook {
				
				struct Patch : CallPatch
				{

					Patch(std::uintptr_t a_callAddr, std::uintptr_t a_retAddr, std::uintptr_t a_ifFreezeLabelAddr)
					{
						Xbyak::Label callLbl;
						Xbyak::Label retLbl;
						Xbyak::Label isFrozen;
						Xbyak::Label ifFreezeLabel;
						Xbyak::Label ifgeInstructionDataBitCountLabel;
						/**
						* The main issue we are trying to solve is that the InstructionPointer on the top stack frame 
						* doesn't get updated until the tasklet actually finishes executing. We need this to be set in order for our subsequent call to work
						* 
						* It doesn't do this until it either:
						* - reaches the end of the instruction bitstream
						* - the stack is about to freeze
						* - the max ops per tasklet have been executed (100)
						* 
						* The actual IP is set in edx before checking for the first two conditions, and this will be used to set the topFrame's IP if the tasklet exits
						* So we need to install our branch right after that to be able to load it into our hook
						* We install into the `jz` instruction since it's a 6-byte long jump.
						* 
						* Here's our hook target, near the start of the main loop:
						* ```
                        *  lea     edx, [rax+rcx*8]								      # at this point, edx holds the actual current IP
						*  cmp     dword ptr [rax+6Ch], 1                             # check if this->stack->freeze state is 1 (frozen)
						*  jz      if_frozen_label                                    # jumps if above comparison is true               <-- branch installed here
						*  cmp     edx, [<rsi/rdi>+40h]                               # compare the current IP to this->InstructionDataBitCount ("(CodeTasklet) this" is rsi in AE, rdi in SE)
						*  jb      short if_less_than_InstructionDataBitCount_label   # jump if the current IP is less than this->InstructionDataBitCount
						* ```
						*
						* Since we hook right in the middle of the checking of the first two conditions, we want to check those before calling our hook,
						* we don't want to block the codetasklet thread if it's about to exit.
						* the current ops count and max ops comparison happens at the end of the loop, so we don't have to check that
						*/
						cmp(dword[rax + 0x6C], 1); // check to see if stack->freeze state is 1 (frozen)
						jz(isFrozen); // we overwrite this instruction, so we have to jump to our saved address
						if (REL::Module::IsAE()) {
							cmp(edx, dword[rsi + 0x40]); // (CodeTasklet)this is rsi in AE
						} else {
							cmp(edx, dword[rdi + 0x40]); // (CodeTasklet)this is rdi in SE
						}
						// originally a `jb` that skips the main switch case; we just want this to return if the above comparison is true
						// we didn't overwrite that jump or the above comparison, so we can just return to the return address
						jge(ifgeInstructionDataBitCountLabel);
						
						saveVolatiles();	// save volatile registers
		
						if (REL::Module::IsAE()) {
							mov(rcx, rsi);	// first param: BSScript::Internal::CodeTasklet*
							// second param: edx is already what we want, the current instruction pointer
						}
						else {
							mov(rcx, rdi);	// first param: rcx = rdi == BSScript::Internal::CodeTasklet*
											// second param: edx is already what we want, the current instruction pointer
						}
						sub(rsp, 0x20); // pad the stack with the amount of parameters that we're going to be using (up to 4 64-bit values)
						call(ptr[rip + callLbl]);	// make call
						add(rsp, 0x20); // put it back

						loadVolatiles();

						L(ifgeInstructionDataBitCountLabel);
						jmp(ptr[rip + retLbl]);	// resume execution

						L(callLbl);
						dq(a_callAddr);

						L(retLbl);
						dq(a_retAddr);
						
						L(isFrozen);
						jmp(ptr[rip + ifFreezeLabel]);

						L(ifFreezeLabel);
						dq(a_ifFreezeLabelAddr);

					}

				};
				static inline void Install()
				{					
					// InstructionExecute
					// 1.5.97:  0x141278110: BSScript__Internal__CodeTasklet::VMProcess_141278110
					// 1.6.640: 0x14139C860: BSScript__Internal__CodeTasklet::sub_14139C860
					// 1_5_97   CAVE_START = 0x170
					// 1_6_640  CAVE_START = 0x14C
					// 1_5_97   CAVE_END   = 0x176
					// 1_6_640  CAVE_END   = 0x153
					// Cave start and cave end indicate the beginning and end of the instructions 
					// We install near the beginning of the loop
					// The installation target is the `jz` instruction that jumps if the freeze state is 1 
					// CAVE_SIZE = 6 
					auto vmprocess_reloc = RELOCATION_ID(98520, 105176);

					//TODO: Find VR offsets, using SE offsets as placeholders
					auto cave_start_var_offset = REL::VariantOffset(0xD6, 0xCA, 0xD6);
					auto cave_end_var_offset = REL::Offset(cave_start_var_offset.offset()+6);

					REL::Relocation<std::uintptr_t> cave_start_reloc{ vmprocess_reloc, cave_start_var_offset };
					REL::Relocation<std::uintptr_t> cave_end_reloc{ vmprocess_reloc, cave_end_var_offset };
					std::size_t CAVE_START = cave_start_var_offset.offset();
					std::size_t CAVE_END = cave_end_var_offset.offset();
					std::size_t CAVE_SIZE = CAVE_END - CAVE_START;

					assert(CAVE_SIZE >= 6);
					// we need to read what the offset is in the `jz` instruction;
					// jz instruction is opcode (`0F 84`) followed by a four-byte offset
					auto if_freeze_label_offset_loc_offset = REL::Offset(cave_start_var_offset.offset() + 2);
					REL::Relocation<std::uintptr_t> if_freeze_label_offset_loc_addr{ vmprocess_reloc, if_freeze_label_offset_loc_offset };
					uint32_t * ptr_to_offset = (uint32_t *)if_freeze_label_offset_loc_addr.address();
					uint32_t offset_val = *ptr_to_offset;
					auto if_freeze_label_offset = REL::Offset(offset_val + cave_end_var_offset.offset()); // the offset relative to the address AFTER the jz instruction
					REL::Relocation<std::uintptr_t> if_freeze_label_address{ vmprocess_reloc, if_freeze_label_offset };

					auto patch = Patch(XSE::stl::unrestricted_cast<std::uintptr_t>(InstructionExecute_Hook), cave_end_reloc.address(), if_freeze_label_address.address());
					auto& trampoline = SKSE::GetTrampoline();
					SKSE::AllocTrampoline(patch.getSize() + 14);
					auto result = trampoline.allocate(patch);
					trampoline.write_branch<6>(cave_start_reloc.address(), (std::uintptr_t)result);
					auto BASE_LOAD_ADDR = vmprocess_reloc.address() - vmprocess_reloc.offset();
					logger::info("Base for executable is: 0x{:X}", BASE_LOAD_ADDR);
					logger::info("InstructionExecute address: 0x{:X}", vmprocess_reloc.address());
					logger::info("InstructionExecute relocation offset: 0x{:X}", vmprocess_reloc.offset());

					logger::info("InstructionExecuteHook hooked at address 0x{:X}", cave_start_reloc.address());
					logger::info("InstructionExecuteHook hooked at offset 0x{:X}", cave_start_reloc.offset());
					logger::info("InstructionExecuteHook if_freeze_label_offset at address 0x{:X}", if_freeze_label_offset.address());
					logger::info("InstructionExecuteHook if_freeze_label_offset at offset 0x{:X}", if_freeze_label_offset.offset());
					logger::info("InstructionExecuteHook:CAVE_START is 0x{:X}", CAVE_START);
					logger::info("InstructionExecuteHook:CAVE_END is 0x{:X}", CAVE_END);
					logger::info("InstructionExecuteHook:CAVE_SIZE is 0x{:X}", CAVE_SIZE);

					std::size_t RESULT_ADDR = (std::uintptr_t)result;
					logger::info("InstructionExecuteHook patch allocation address: 0x{:X}", RESULT_ADDR);
					logger::info("InstructionExecuteHook patch allocation offset: 0x{:X}", RESULT_ADDR - BASE_LOAD_ADDR);

				}
			};


			void CommitHooks()
			{
				InstructionExecuteHook::Install();

				{
					// CreateStack
					// 1.5.29:  0x1412641F0: BSScript__Internal__VirtualMachine::sub_1412641F0 
					// 1_6_640: 0x14138AD20: BSScript__Internal__VirtualMachine::sub_14138AD20
					// 1_5_97   HOOK_TARGET = 0x1D4;
					// 1_6_640  HOOK_TARGET = 0x1D9;
					//TODO: Find VR offsets, using SE offsets as placeholders
					auto create_stack_reloc = RELOCATION_ID(98146, 104870);
					auto create_stack_hook_target_offset = REL::VariantOffset(0x1D4, 0x1D9, 0x1D4);
					REL::Relocation<std::uintptr_t> create_stack_hook_target{ create_stack_reloc, create_stack_hook_target_offset};

					struct Patch : Xbyak::CodeGenerator
					{
						Patch(std::uintptr_t a_funcAddr)
						{
							Xbyak::Label funcLbl;

							mov(rcx, ptr[rsp - 0x448]);	// rsp - 0x448 == BSTSmartPointer<BSScript::Stack>&
							jmp(ptr[rip + funcLbl]);

							L(funcLbl);
							dq(a_funcAddr);
						}
					};

					auto patch = Patch(XSE::stl::unrestricted_cast<std::uintptr_t>(CreateStack_Hook));
					auto& trampoline = SKSE::GetTrampoline();
					SKSE::AllocTrampoline(patch.getSize() + 14);
					auto result = trampoline.allocate(patch);
					trampoline.write_branch<5>(create_stack_hook_target.address(), (std::uintptr_t)result);
					logger::info("CreateStack address: 0x{:X}", create_stack_reloc.address());
					logger::info("CreateStack relocation offset: 0x{:X}", create_stack_reloc.offset());

					logger::info("CreateStackHook hooked at address 0x{:X}", create_stack_hook_target.address());
					logger::info("CreateStackHook hooked at offset 0x{:X}", create_stack_hook_target.offset());
					logger::info("CreateStackHook:HOOK_OFFSET is 0x{:X}", create_stack_hook_target_offset.offset());

					std::size_t RESULT_ADDR = (std::uintptr_t)result;
					logger::info("CreateStackHook patch allocation address: 0x{:X}", RESULT_ADDR);
				}


				{
					// CleanupStack 
 					// 1.5.97:  0x1412646F0: BSScript__Internal__VirtualMachine::sub_1412646F0
					// 1.6.640: 0x14138B180: BSScript__Internal__VirtualMachine::sub_14138B180
					// CAVE_START = 0x0; 	// 1_5_97 and 1_6_640
					// CAVE_END = 0x9; 	// 1_5_97 and 1_6_640
					// CAVE_SIZE = 9
					// TODO: Find VR offsets (they should be the same since SE and AE are the same)
					auto cave_start_var_offset = REL::VariantOffset(0x0, 0x0, 0x0);
					auto cave_end_var_offset = REL::VariantOffset(0x9, 0x9, 0x9);
					REL::Relocation<std::uintptr_t> func_base_reloc{ RELOCATION_ID(98149, 104873) };
					REL::Relocation<std::uintptr_t> cave_start_reloc{ RELOCATION_ID(98149, 104873), cave_start_var_offset };
					REL::Relocation<std::uintptr_t> cave_end_reloc{ RELOCATION_ID(98149, 104873), cave_end_var_offset };
					std::size_t CAVE_START = cave_start_var_offset.offset();
					std::size_t CAVE_END = cave_end_var_offset.offset();
					std::size_t CAVE_SIZE = CAVE_END - CAVE_START;
					struct Patch : Xbyak::CodeGenerator
					{
						Patch(std::size_t a_funcAddr)
						{
							Xbyak::Label funcLbl;

							jmp(ptr[rip + funcLbl]);

							L(funcLbl);
							dq(a_funcAddr);
						}
					};
					auto patch = Patch(XSE::stl::unrestricted_cast<std::uintptr_t>(CleanupStack_Hook));
					auto& trampoline = SKSE::GetTrampoline();
					SKSE::AllocTrampoline(patch.getSize() + 14);
					auto result = trampoline.allocate(patch);
					trampoline.write_branch<6>(cave_start_reloc.address(), (std::uintptr_t)result);

					assert(CAVE_SIZE >= 6);
					// A write_branch<6> writes a 6 byte branch to the address we want; if there's more than 6 bytes we have to skip over, we have to nop it out
					REL::safe_fill(func_base_reloc.address() + CAVE_START + 6, REL::NOP, CAVE_END - (CAVE_START + 6));

					_CleanupStack = reinterpret_cast<CleanupStack_t*>(cave_end_reloc.address());
					logger::info("CleanupStack address: 0x{:X}", func_base_reloc.address());
					logger::info("CleanupStack relocation offset: 0x{:X}", func_base_reloc.offset());

					logger::info("CleanupStackHook hooked at address 0x{:X}", cave_start_reloc.address());
					logger::info("CleanupStackHook hooked at offset 0x{:X}", cave_start_reloc.offset());
					logger::info("CleanupStackHook:CAVE_START is 0x{:X}", cave_start_var_offset.offset());
					logger::info("CleanupStackHook:CAVE_END is 0x{:X}", cave_end_var_offset.offset());

					std::size_t RESULT_ADDR = (std::uintptr_t)result;
					logger::info("CleanupStackHook patch allocation address: 0x{:X}", RESULT_ADDR);


				}

				RE::BSScript::Internal::VirtualMachine::GetSingleton()->RegisterForLogEvent(new LogEventSink());
				// RE::ScriptEventSourceHolder::GetSingleton()->AddEventSink(new ScriptInitEventSink());
			}
		}
#elif FALLOUT
		//BSScript::Internal::CodeTasklet::Process 14276E9E0
		auto vmprocess_reloc = REL::ID(614585);
   	//hooks at 14276EADD
		auto cave_start_offset = REL::Offset(0xFD);
		// REL::ID(614585) + REL::Offset(0xFD) == 14276EADD
		REL::Relocation<uintptr_t> InstructionExecute(vmprocess_reloc,cave_start_offset.offset());
		void InstructionExecute_Hook(RE::BSScript::Internal::CodeTasklet* tasklet, RE::BSScript::Internal::CodeTasklet::OpCode opCode)
		{
			if (tasklet->topFrame)
			{
				// We don't need to set the instruction pointer because Fallout 4 assigns the IP every time an opcode is executed
				g_InstructionExecutionEvent(tasklet, tasklet->topFrame->STACK_FRAME_IP);
			}
		}
		// TODO: There's a second CreateStack() @ 1427422C0, do we need to hook that?
		// Probably not, it's only used by LoadStackTable() and the first CreateStack*()
		typedef bool (*_CreateStack)(RE::BSScript::Internal::VirtualMachine* vm, std::uint64_t unk1, std::uint64_t unk2, std::uint64_t unk3, RE::BSTSmartPointer<RE::BSScript::Stack>& stack);
		REL::Relocation<_CreateStack> CreateStack(REL::ID(107558)); //REL::ID(107558)
		_CreateStack CreateStack_Original = nullptr;

		bool CreateStack_Hook(RE::BSScript::Internal::VirtualMachine* vm, std::uint64_t unk1, std::uint64_t unk2, std::uint64_t unk3, RE::BSTSmartPointer<RE::BSScript::Stack>& stack)
		{
			bool result = CreateStack_Original(vm, unk1, unk2, unk3, stack);

			if (result)
			{
				g_CreateStackEvent(stack);
			}

			return result;
		}
		typedef void (*_CleanupStack)(RE::BSScript::Internal::VirtualMachine* vm, RE::BSScript::Stack* stack);
		REL::Relocation<_CleanupStack> CleanupStack(REL::ID(38511)); //REL::ID(38511)
		_CleanupStack CleanupStack_Original = nullptr;

		void CleanupStack_Hook(RE::BSScript::Internal::VirtualMachine* vm, RE::BSScript::Stack* stack)
		{
			if (!stack)
			{
				return;
			}

			CleanupStack_Original(vm, stack);
			g_CleanupStackEvent(stack->stackID);
		}

		namespace Internal
		{
			void CommitHooks()
			{
				{
					struct InstructionExecute_Code : Xbyak::CodeGenerator {
						InstructionExecute_Code(uintptr_t funcAddr)
						{
							Xbyak::Label funcLabel;
							Xbyak::Label retnLabel;

							and_(edx, 0x3f);
							and_(eax, 0x3f);
							mov(dword[rbp + 0x30], edx);
							mov(qword[rbp - 0x38], rax);
							lea(rax, ptr[r9 + r8 * 8]);
							mov(qword[rbp - 0x40], rax);

							push(rax);
							push(rcx);
							push(rdx);
							push(r8);
							push(r9);
							push(r10);
							push(r11);
							push(r11);

							mov(rcx, rdi);
							mov(rdx, dword[rbp + 0x30]);

							sub(rsp, 0x20);
							call(ptr[rip + funcLabel]);
							add(rsp, 0x20);

							pop(r11);
							pop(r11);
							pop(r10);
							pop(r9);
							pop(r8);
							pop(rdx);
							pop(rcx);
							pop(rax);

							jmp(ptr[rip + retnLabel]);

							L(funcLabel);
							dq(funcAddr);

							L(retnLabel);
							dq(InstructionExecute.address() + 0x15);
						}
					};
					auto patch = InstructionExecute_Code(XSE::stl::unrestricted_cast<std::uintptr_t>(InstructionExecute_Hook));
					auto& trampoline = XSE::GetTrampoline();
					XSE::AllocTrampoline(patch.getSize() + 14);
					auto result = trampoline.allocate(patch);
					trampoline.write_branch<6>(InstructionExecute.address(), (std::uintptr_t)result);

					// void* codeBuf = g_localTrampoline.StartAlloc();
					// InstructionExecute_Code code(codeBuf, (uintptr_t)InstructionExecute_Hook);
					// g_localTrampoline.EndAlloc(code.getCurr());

					// g_branchTrampoline.Write6Branch(InstructionExecute, uintptr_t(code.getCode()));
				}

				{
					struct CreateStack_Code : Xbyak::CodeGenerator {
						CreateStack_Code() 
						{
							Xbyak::Label retnLabel;

							mov(ptr[rsp + 0x10], rbx);

							jmp(ptr[rip + retnLabel]);

							L(retnLabel);
							dq(CreateStack.address() + 5);
						}
					};
					auto patch = CreateStack_Code();
					auto& trampoline = XSE::GetTrampoline();
					XSE::AllocTrampoline(patch.getSize() + 14);
					auto result = trampoline.allocate(patch);
					CreateStack_Original = (_CreateStack)result;
					trampoline.write_branch<5>(CreateStack.address(), (std::uintptr_t)CreateStack_Hook);

					// void* codeBuf = g_localTrampoline.StartAlloc();
					// CreateStack_Code code(codeBuf);
					// g_localTrampoline.EndAlloc(code.getCurr());

					// CreateStack_Original = (_CreateStack)codeBuf;

					// g_branchTrampoline.Write5Branch(CreateStack.address(), (uintptr_t)CreateStack_Hook);
				}

				{
					struct CleanupStack_Code : Xbyak::CodeGenerator {
						CleanupStack_Code()
						{
							Xbyak::Label retnLabel;
							Xbyak::Label jmpLabel;

							jmp(ptr[rip + retnLabel]);

							L(retnLabel);
							dq(CleanupStack.address() + 9);
						}
					};
					auto patch = CleanupStack_Code();
					auto& trampoline = XSE::GetTrampoline();
					XSE::AllocTrampoline(patch.getSize() + 14);
					auto result = trampoline.allocate(patch);
					CleanupStack_Original = (_CleanupStack) result;
					trampoline.write_branch<5>(CleanupStack.address(), (std::uintptr_t)CleanupStack_Hook);

					// void* codeBuf = g_localTrampoline.StartAlloc();
					// CleanupStack_Code code(codeBuf);
					// g_localTrampoline.EndAlloc(code.getCurr());

					// CleanupStack_Original = (_CleanupStack)codeBuf;

					// g_branchTrampoline.Write5Branch(CleanupStack.GetUIntPtr(), (uintptr_t)CleanupStack_Hook);
				}
	
				RE::BSScript::Internal::VirtualMachine::GetSingleton()->RegisterForLogEvent(new LogEventSink());
				
				//GetEventDispatcher<TESInitScriptEvent>()->AddEventSink(
				//	reinterpret_cast<BSTEventSink<TESInitScriptEvent>*>(new ScriptInitEventSink()));
			}
		}
#endif
		

	void EmitBreakpointChangedEvent(const dap::Breakpoint& bpoint, const std::string& what)
	{
		g_BreakpointChangedEvent(bpoint, what);
	}

}
}
