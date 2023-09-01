#pragma once
#include <map>
#include <set>
#include <dap/protocol.h>
#include <dap/session.h>

#include "GameInterfaces.h"

#include "PexCache.h"

namespace DarkId::Papyrus::DebugServer
{
	class BreakpointManager
	{

	public:
		struct BreakpointInfo {
			int64_t breakpointId;
			int instructionNum;
			int lineNum;
			int debugFuncInfoIndex;
		};

		struct ScriptBreakpoints {
			int ref{ -1 };
			dap::Source source;
			std::time_t modificationTime{ 0 };
			std::map<int, BreakpointInfo> breakpoints;
			
		};

		explicit BreakpointManager(PexCache* pexCache)
			: m_pexCache(pexCache)
		{
		}

		dap::ResponseOrError<dap::SetBreakpointsResponse> SetBreakpoints(const dap::Source& src, const std::vector<dap::SourceBreakpoint>& srcBreakpoints);
		void ClearBreakpoints(bool emitChanged = false);
		bool CheckIfFunctionWillWaitOrExit(RE::BSScript::Internal::CodeTasklet* tasklet);
		void InvalidateAllBreakpointsForScript(int ref);
		bool GetExecutionIsAtValidBreakpoint(RE::BSScript::Internal::CodeTasklet* tasklet);
	private:
		PexCache* m_pexCache;
		std::map<int, ScriptBreakpoints> m_breakpoints;

	};
}
