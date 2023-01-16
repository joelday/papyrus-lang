#pragma once

#include "GameInterfaces.h"

#include "Protocol/protocol.h"
#include "StateNodeBase.h"

namespace DarkId::Papyrus::DebugServer
{
	class ArrayStateNode : public StateNodeBase, public IProtocolVariableSerializable, public IStructuredState
	{
		std::string m_name;

		RE::BSTSmartPointer<RE::BSScript::Array> m_value;
		RE::BSScript::TypeInfo* m_type;
		RE::BSScript::TypeInfo _m_inst_type;
	public:
		ArrayStateNode(std::string name, RE::BSScript::Array* value, RE::BSScript::TypeInfo* type);
		ArrayStateNode(std::string name, RE::BSScript::Array* value, const RE::BSScript::TypeInfo &type);

		virtual ~ArrayStateNode() override = default;

		bool SerializeToProtocol(Variable& variable) override;

		bool GetChildNames(std::vector<std::string>& names) override;
		bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) override;
	};
}