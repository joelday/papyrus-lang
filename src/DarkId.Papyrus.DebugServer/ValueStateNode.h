#pragma once

#include "GameInterfaces.h"

#include "Protocol/protocol.h"
#include "StateNodeBase.h"

namespace DarkId::Papyrus::DebugServer
{
	class ValueStateNode : public StateNodeBase, public IProtocolVariableSerializable
	{
		std::string m_name;
		const RE::BSScript::Variable* m_variable;

	public:
		ValueStateNode(std::string name, const RE::BSScript::Variable* variable);
		bool SerializeToProtocol(Variable& variable) override;
	};
}
