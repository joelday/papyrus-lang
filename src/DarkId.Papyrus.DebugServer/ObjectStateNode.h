#pragma once

#include "GameInterfaces.h"
#include <dap/protocol.h>

#include "StateNodeBase.h"
#include <map>

namespace DarkId::Papyrus::DebugServer
{
	class ObjectStateNode : public StateNodeBase, public IProtocolVariableSerializable, public IStructuredState
	{
		std::string m_name;
		bool m_subView;
		
		RE::BSTSmartPointer<RE::BSScript::Object> m_value;
		RE::BSTSmartPointer<RE::BSScript::ObjectTypeInfo> m_class;
	public:
		ObjectStateNode(std::string name, RE::BSScript::Object* value, RE::BSScript::ObjectTypeInfo* asClass, bool subView = false);

		bool SerializeToProtocol(dap::Variable& variable) override;

		bool GetChildNames(std::vector<std::string>& names) override;
		bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) override;
	};
}
