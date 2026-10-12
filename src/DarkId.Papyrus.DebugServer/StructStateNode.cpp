#if FALLOUT

#include "StructStateNode.h"
#include "RuntimeState.h"

namespace DarkId::Papyrus::DebugServer
{
	StructStateNode::StructStateNode(std::string name, RE::BSScript::Struct* value, RE::BSScript::StructTypeInfo* knownType) : m_name(name), m_value(value)
	{
		m_type = RE::BSTSmartPointer<RE::BSScript::StructTypeInfo>(value ? value->type.get() : knownType);
	}

	bool StructStateNode::SerializeToProtocol(dap::Variable& variable)
	{
		variable.variablesReference = m_value ? GetId() : 0;
		variable.namedVariables = m_value ? m_type->variables.size() : 0;
		
		variable.name = m_name;
		variable.type = m_type->GetName();
		variable.value = m_value ? m_type->GetName() : "NONE";
		
		return true;
	}

	bool StructStateNode::GetChildNames(std::vector<std::string>& names)
	{
		if (!m_value)
		{
			return true;
		}
		
		for (const auto pair : m_type->varNameIndexMap)
		{
			names.push_back(pair.first.c_str());
		}

		return true;
	}

	bool StructStateNode::GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node)
	{
		if (!m_value)
		{
			return false;
		}
		
		const auto propIndex = m_type->varNameIndexMap.find(RE::BSFixedString(name));
		if (propIndex == m_type->varNameIndexMap.end())
		{
			return false;
		}

		const auto propValue = &m_value->variables[propIndex->second];
		node = RuntimeState::CreateNodeForVariable(propIndex->first.c_str(), propValue);
		
		return true;
	}
}
#endif