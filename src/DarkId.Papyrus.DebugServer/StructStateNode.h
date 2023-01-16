#pragma once
#if FALLOUT
#include "GameInterfaces.h"

#include "Protocol/protocol.h"
#include "StateNodeBase.h"

namespace DarkId::Papyrus::DebugServer
{
	class StructStateNode : public StateNodeBase, public IProtocolVariableSerializable, public IStructuredState
	{
		std::string m_name;

		RE::BSTSmartPointer<RE::BSScript::Struct> m_value;
		RE::BSTSmartPointer<RE::BSScript::StructTypeInfo> m_type;
	public:
		StructStateNode(std::string name, RE::BSScript::Struct* value, RE::BSScript::StructTypeInfo* knownType);

		bool SerializeToProtocol(Variable& variable) override;

		bool GetChildNames(std::vector<std::string>& names) override;
		bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) override;
	};
}
#endif