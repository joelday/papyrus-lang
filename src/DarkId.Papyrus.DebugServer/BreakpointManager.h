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
		std::map<int, std::set<int>> m_breakpoints;
		PexCache* m_pexCache;

	public:
		explicit BreakpointManager(PexCache* pexCache)
			: m_pexCache(pexCache)
		{
		}

		dap::ResponseOrError<dap::SetBreakpointsResponse> SetBreakpoints(const dap::Source& src, const std::vector<dap::SourceBreakpoint>& srcBreakpoints);
		void ClearBreakpoints();
		bool GetExecutionIsAtValidBreakpoint(RE::BSScript::Internal::CodeTasklet* tasklet, uint32_t actualIP);
	};
}
