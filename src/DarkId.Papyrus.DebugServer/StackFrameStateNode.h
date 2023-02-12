#pragma once

#include "GameInterfaces.h"

#include <dap/protocol.h>
#include "PexCache.h"
#include "StateNodeBase.h"

namespace DarkId::Papyrus::DebugServer
{
	class StackFrameStateNode : public StateNodeBase, public IStructuredState
	{
		RE::BSScript::StackFrame* m_stackFrame;

	public:
		explicit StackFrameStateNode(RE::BSScript::StackFrame* stackFrame);

		bool SerializeToProtocol(dap::StackFrame& stackFrame, PexCache* pexCache) const;

		bool GetChildNames(std::vector<std::string>& names) override;
		bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) override;
	};
}
