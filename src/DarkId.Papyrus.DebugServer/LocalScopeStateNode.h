#pragma once

#include "GameInterfaces.h"
#include "Protocol/protocol.h"

#include "StateNodeBase.h"

namespace DarkId::Papyrus::DebugServer
{
	class LocalScopeStateNode : public StateNodeBase, public IProtocolScopeSerializable, public IStructuredState
	{
		RE::BSScript::StackFrame* m_stackFrame;

	public:
		LocalScopeStateNode(RE::BSScript::StackFrame* stackFrame);

		bool SerializeToProtocol(Scope& scope) override;
		bool GetChildNames(std::vector<std::string>& names) override;
		bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) override;
	};
}
