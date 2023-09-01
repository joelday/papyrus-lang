#include "BreakpointManager.h"
#include <Champollion/Pex/Binary.hpp>
#include <regex>
#include "Utilities.h"
#ifdef _DEBUG_DUMP_PEX
#include "Pex.h"
#endif
namespace DarkId::Papyrus::DebugServer
{
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

		for (const auto& srcBreakpoint : srcBreakpoints)
		{
			auto foundLine = false;
			int line = static_cast<int>(srcBreakpoint.line);
			if (binary)
			{
				for (auto & functionInfo : binary->getDebugInfo().getFunctionInfos())
				{
					if (foundLine)
					{
						break;
					}

					for (auto lineNumber : functionInfo.getLineNumbers())
					{
						if (line == static_cast<int>(lineNumber))
						{
							foundLine = true;
							break;
						}
					}
				}
			}

			breakpointLines.emplace(line);

			response.breakpoints.push_back(dap::Breakpoint{
				.line = dap::integer(line),
				.source = source,
				.verified = foundLine,
				});
		}

		m_breakpoints[ref] = breakpointLines;
		return response;
	}
	void BreakpointManager::ClearBreakpoints() {
		m_breakpoints.clear();
	}
	bool BreakpointManager::GetExecutionIsAtValidBreakpoint(RE::BSScript::Internal::CodeTasklet* tasklet, uint32_t actualIP)
	{
		auto & func = tasklet->topFrame->owningFunction;

		if (func->GetIsNative())
		{
			return false;
		}
		const auto sourceReference = GetScriptReference(tasklet->topFrame->owningObjectType->GetName());
		
		if (m_breakpoints.find(sourceReference) != m_breakpoints.end())
		{
			auto & breakpointLines = m_breakpoints[sourceReference];
			if (!breakpointLines.empty())
			{
				uint32_t currentLine;
				bool success = func->TranslateIPToLineNumber(actualIP, currentLine);
				if (success && breakpointLines.find(currentLine) != breakpointLines.end()) {
					return true;
				}
				return false;
			}
		}

		return false;
	}
}
