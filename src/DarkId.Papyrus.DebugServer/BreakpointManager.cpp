#include "BreakpointManager.h"
#include <Champollion/Pex/Binary.hpp>
#include <regex>
#include "Utilities.h"
#ifdef _DEBUG_DUMP_PEX
#include "Pex.h"
#endif
namespace DarkId::Papyrus::DebugServer
{
	dap::ResponseOrError<dap::SetBreakpointsResponse> BreakpointManager::SetBreakpoints(dap::Source& source, const std::vector<dap::SourceBreakpoint>& srcBreakpoints)
	{
		dap::SetBreakpointsResponse response;
		std::set<int> breakpointLines;
		
		auto scriptName = NormalizeScriptName(source.name.value(""));
		auto binary = m_pexCache->GetScript(scriptName.c_str());
		if (!binary) {
			return dap::Error("Could not find PEX data for script %s", scriptName);
		}
		const auto sourceReference = m_pexCache->GetScriptReference(scriptName.c_str());
		source.sourceReference = sourceReference;
		
#if _DEBUG_DUMP_PEX
		std::string dir = logger::log_directory().value_or("").string();
		if (dir.empty()) {
			logger::error("Failed to open log directory, PEX will not be dumped");
		}
		else if (!LoadAndDumpPexData(scriptName.c_str(), dir)) {
			logger::error("Could not save PEX dump for {}"sv, scriptName);
		}
#endif
		bool hasDebugInfo = binary->getDebugInfo().getFunctionInfos().size() > 0;
		if (!hasDebugInfo) {
			return dap::Error("Could not find PEX data for script %s", scriptName);
		}

		for (const auto& srcBreakpoint : srcBreakpoints)
		{
			auto foundLine = false;

			if (binary)
			{
				for (auto functionInfo : binary->getDebugInfo().getFunctionInfos())
				{
					if (foundLine)
					{
						break;
					}

					for (auto lineNumber : functionInfo.getLineNumbers())
					{
						if (srcBreakpoint.line == lineNumber)
						{
							foundLine = true;
							break;
						}
					}
				}
			}

			breakpointLines.emplace(srcBreakpoint.line);

			dap::Breakpoint breakpoint;
			breakpoint.source = source;
			breakpoint.verified = foundLine;
			breakpoint.line = srcBreakpoint.line;

			response.breakpoints.push_back(breakpoint);
		}

		m_breakpoints[sourceReference] = breakpointLines;
		return response;
	}

	bool BreakpointManager::GetExecutionIsAtValidBreakpoint(RE::BSScript::Internal::CodeTasklet* tasklet)
	{
		auto func = tasklet->topFrame->owningFunction;

		if (func->GetIsNative())
		{
			return false;
		}
		const auto scriptName = tasklet->topFrame->owningObjectType->GetName();
		const auto sourceReference = m_pexCache->GetScriptReference(scriptName);
		
		if (m_breakpoints.find(sourceReference) != m_breakpoints.end())
		{
			auto breakpointLines = m_breakpoints[sourceReference];
			if (!breakpointLines.empty())
			{
				uint32_t currentLine;
				#ifdef SKYRIM
				bool success = func->TranslateIPToLineNumber(tasklet->topFrame->instructionPointer, currentLine);
				#else // FAllOUT
				bool success = func->TranslateIPToLineNumber(tasklet->topFrame->ip, currentLine);
				#endif
				auto found = breakpointLines.find(currentLine);
				return success && found != breakpointLines.end();
			}
		}

		return false;
	}
}
