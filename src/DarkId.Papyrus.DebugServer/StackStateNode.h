#pragma once

#include "GameInterfaces.h"

#include <dap/protocol.h>
#include "StateNodeBase.h"

namespace DarkId::Papyrus::DebugServer
{
	class StackStateNode : public StateNodeBase, public IStructuredState
	{
		uint32_t m_stackId;

	public:
		StackStateNode(uint32_t stackId);

		bool SerializeToProtocol(dap::Thread& thread) const;

		bool GetChildNames(std::vector<std::string>& names) override;
		bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) override;
	};
}
