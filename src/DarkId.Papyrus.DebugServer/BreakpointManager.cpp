#include "BreakpointManager.h"
#include <Champollion/Pex/Binary.hpp>
#include <regex>
#include "Utilities.h"
#include "Pex.h"
#include "ConfigHooks.h"
#include "RuntimeEvents.h"
#include "GameInterfaces.h"

namespace DarkId::Papyrus::DebugServer
{

	int64_t GetBreakpointID(int scriptReference, int lineNumber) {
		return (((int64_t)scriptReference) << 32) + lineNumber;
	}

	std::string GetInstructionReference(const Pex::DebugInfo::FunctionInfo& finfo) {
		return std::format("{}:{}:{}", finfo.getObjectName().asString(), finfo.getStateName().asString(), finfo.getFunctionName().asString());
	}

	dap::ResponseOrError<dap::SetBreakpointsResponse> BreakpointManager::SetBreakpoints(const dap::Source& source, const std::vector<dap::SourceBreakpoint>& srcBreakpoints)
	{
		dap::SetBreakpointsResponse response;
		std::set<int> breakpointLines;
		auto scriptName = NormalizeScriptName(source.name.value(""));
		auto binary = m_pexCache->GetScript(scriptName.c_str());
		if (!binary) {
			RETURN_DAP_ERROR(std::format("SetBreakpoints: Could not find PEX data for script {}", scriptName));
		}
		auto ref = GetSourceReference(source);
		bool hasDebugInfo = binary->getDebugInfo().getFunctionInfos().size() > 0;
		
		if (!hasDebugInfo) {
#if FALLOUT
			const std::string iniName = "fallout4.ini";
#else
			const std::string iniName = "skyrim.ini";
#endif
			RETURN_DAP_ERROR(std::format("SetBreakpoints: No debug data for script {}. Ensure that `bLoadDebugInformation=1` is set under `[Papyrus]` in {}", scriptName, iniName));
		}

		ScriptBreakpoints info {
		   .ref = ref,
		   .source = source,
		   .modificationTime = binary->getDebugInfo().getModificationTime()
		};
		std::map<int, BreakpointInfo> foundBreakpoints;
		
		for (const auto& srcBreakpoint : srcBreakpoints)
		{
			auto foundLine = false;
			int line = static_cast<int>(srcBreakpoint.line);
			int instructionNum = -1;
			int foundFunctionInfoIndex{-1};
			Pex::DebugInfo::FunctionInfo debugfinfo;
			int64_t breakpointId = -1;
			if (binary)
			{
				auto& funcInfos = binary->getDebugInfo().getFunctionInfos();
				for (int j = 0; j < funcInfos.size(); j++)
				{
					if (foundLine)
					{
						break;
					}
					for (int i = 0; i < funcInfos[j].getLineNumbers().size(); i++)
					{
						auto lineNumber = funcInfos[j].getLineNumbers()[i];
						if (line == static_cast<int>(lineNumber))
						{
							foundLine = true;
							instructionNum = i;
							foundFunctionInfoIndex = j;
							debugfinfo = funcInfos[j];
							break;
						}
					}
				}
			}
			breakpointId = GetBreakpointID(ref, line);

			if (foundLine) {
				auto bpoint = BreakpointInfo{
					.breakpointId = breakpointId,
					.instructionNum = instructionNum,
					.lineNum = line,
					.debugFuncInfoIndex = foundFunctionInfoIndex
				};
				info.breakpoints[instructionNum] = bpoint;
			}

			response.breakpoints.push_back( dap::Breakpoint {
				.id = foundLine ? dap::integer(breakpointId) : dap::optional<dap::integer>(),
				.instructionReference = foundLine ? GetInstructionReference(debugfinfo) : dap::optional<dap::string>(),
				.line = dap::integer(line),
				.offset = foundLine ? dap::integer(instructionNum) : dap::optional<dap::integer>(),
				.source = source,
				.verified = foundLine
				});
		}

		m_breakpoints[ref] = info;
		return response;
	}

	void BreakpointManager::ClearBreakpoints(bool emitChanged) {
		if (emitChanged) {
			for (auto & kv : m_breakpoints) {
				InvalidateAllBreakpointsForScript(kv.first);
			}
		}
		m_breakpoints.clear();
	}

	// TODO: Upstream this
	uint32_t GetInstructionNumberForOffset(RE::BSScript::ByteCode::PackedInstructionStream* stream, uint32_t IP) {
		using func_t = decltype(&GetInstructionNumberForOffset);
		REL::Relocation<func_t> func{ Game_Offset::GetInstructionNumberForOffset };
		return func(stream, IP);
	}

	void BreakpointManager::InvalidateAllBreakpointsForScript(int ref) {
		if (m_breakpoints.find(ref) != m_breakpoints.end())
		{
			return;
		}
		for (auto& KV : m_breakpoints[ref].breakpoints) 
		{
			auto bpinfo = KV.second;
			RuntimeEvents::EmitBreakpointChangedEvent(dap::Breakpoint{
				.id = bpinfo.breakpointId,
				.line = bpinfo.lineNum,
				.source = m_breakpoints[ref].source,
				.verified = false
				}, "changed");
		}
		m_breakpoints.erase(ref);
	}

	bool BreakpointManager::GetExecutionIsAtValidBreakpoint(RE::BSScript::Internal::CodeTasklet* tasklet)
	{
		auto &_func = tasklet->topFrame->owningFunction;
		if (!_func || _func->GetIsNative())
		{
			return false;
		}
		// only ScriptFunctions are non-native
		auto func = static_cast<RE::BSScript::Internal::ScriptFunction*>(_func.get());
		const auto sourceReference = GetScriptReference(tasklet->topFrame->owningObjectType->GetName());
		
		if (m_breakpoints.find(sourceReference) != m_breakpoints.end())
		{
			auto& scriptBreakpoints = m_breakpoints[sourceReference];

			auto binary = m_pexCache->GetCachedScript(sourceReference);
			if (!binary || binary->getDebugInfo().getModificationTime() != scriptBreakpoints.modificationTime) {
				// script was reloaded or removed after placement, remove it
				InvalidateAllBreakpointsForScript(sourceReference);
				return false;
			}
			if (!scriptBreakpoints.breakpoints.empty())
			{
				int currentInstruction = -1;
				auto ip = tasklet->topFrame->STACK_FRAME_IP;
				currentInstruction = GetInstructionNumberForOffset(&func->instructions, ip);
				if (currentInstruction != -1 && scriptBreakpoints.breakpoints.find(currentInstruction) != scriptBreakpoints.breakpoints.end()) {
					return true;
				}
				return false;
			}
		}

		return false;
	}

	//TODO: WIP
	bool BreakpointManager::CheckIfFunctionWillWaitOrExit(RE::BSScript::Internal::CodeTasklet* tasklet) {
		auto& func = tasklet->topFrame->owningFunction;

		if (func->GetIsNative())
		{
			return true;
		}
		auto realfunc = dynamic_cast<RE::BSScript::Internal::ScriptFunction*>(func.get());

		int instNum = GetInstructionNumberForOffset(&realfunc->instructions, tasklet->topFrame->STACK_FRAME_IP);

		std::string scriptName(tasklet->topFrame->owningObjectType->GetName());
		const auto sourceReference = GetScriptReference(scriptName);
		if (m_breakpoints.find(sourceReference) != m_breakpoints.end())
		{
			auto& scriptBreakpoints = m_breakpoints[sourceReference];
			auto binary = m_pexCache->GetScript(scriptName);
			if (!binary || binary->getDebugInfo().getModificationTime() != scriptBreakpoints.modificationTime) {
				return true;
			}
			if (scriptBreakpoints.breakpoints.find(instNum) != scriptBreakpoints.breakpoints.end())
			{

				auto& breakpointInfo = scriptBreakpoints.breakpoints[instNum];
				auto& debugFuncInfo = binary->getDebugInfo().getFunctionInfos()[breakpointInfo.debugFuncInfoIndex];
				auto& lineNumbers = debugFuncInfo.getLineNumbers();
				auto funcData = GetFunctionData(binary, debugFuncInfo.getObjectName(), debugFuncInfo.getStateName(), debugFuncInfo.getFunctionName());
				auto& instructions = funcData->getInstructions();
				for (int i = instNum+1; i < instructions.size(); i++) {
					auto& instruction = instructions[i];
					auto opcode = instruction.getOpCode();
					// TODO: The rest of this
					
				}
			}
		}
		return true;
	}
}
