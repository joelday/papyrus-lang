#include "BreakpointManager.h"
#include <Champollion/Pex/Binary.hpp>
#include <regex>
#include "Utilities.h"
#ifdef _DEBUG_DUMP_PEX
#include "Pex.h"
#endif
namespace DarkId::Papyrus::DebugServer
{
	PDError BreakpointManager::SetBreakpoints(Source& source, const std::vector<SourceBreakpoint>& srcBreakpoints, std::vector<Breakpoint>& breakpoints)
	{
		std::set<int> breakpointLines;

		auto scriptName = NormalizeScriptName(source.name);
		auto binary = m_pexCache->GetScript(scriptName.c_str());
		if (!binary) {
			logger::error("Could not find PEX data for script {}", scriptName);
		} // Continue on to set the breakpoints as unverified
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
		bool hasDebugInfo = binary && binary->getDebugInfo().getFunctionInfos().size() > 0;
		// only log error if PEX is loaded
		if (binary && !hasDebugInfo) {
			logger::error("No debug info in script {}"sv, scriptName);
		} // Continue on to set the breakpoints as unverified

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

			Breakpoint breakpoint;
			breakpoint.source = source;
			breakpoint.verified = foundLine;
			breakpoint.line = srcBreakpoint.line;

			breakpoints.push_back(breakpoint);
		}

		m_breakpoints[sourceReference] = breakpointLines;
		if (!binary) {
			return PDError::NO_PEX_DATA;
		}
		else if (!hasDebugInfo) {
			return PDError::NO_DEBUG_INFO;
		}
		return PDError::OK;
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
