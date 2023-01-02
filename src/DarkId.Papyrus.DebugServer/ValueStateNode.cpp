#include "ValueStateNode.h"
#include "Utilities.h"

namespace DarkId::Papyrus::DebugServer
{
	ValueStateNode::ValueStateNode(std::string name, const RE::BSScript::Variable* variable) :
		m_name(name), m_variable(variable)
	{
	}

	bool ValueStateNode::SerializeToProtocol(Variable& variable)
	{
		variable.name = m_name;
		#if SKYRIM
		if (m_variable->IsString()){
				variable.type = "string";
				auto str = std::string(m_variable->GetString());
				variable.value = StringFormat("\"%s\"", str.c_str());
		} else if (m_variable->IsInt()){
				variable.type = "int";
				variable.value = StringFormat("%d", m_variable->GetSInt());
		} else if (m_variable->IsFloat()){
				variable.type = "float";
				variable.value = StringFormat("%f", m_variable->GetFloat());
		} else if (m_variable->IsBool()){
				variable.type = "bool";
				variable.value = StringFormat(m_variable->GetBool() ? "true" : "false");
		}
		#else
		if (m_variable->is<RE::BSFixedString>()){
				variable.type = "string";
				variable.value = StringFormat("\"%s\"", RE::BSScript::get<RE::BSFixedString>(*m_variable).c_str());
		} else if (m_variable->is<int32_t>()){
				variable.type = "int";
				variable.value = StringFormat("%d", RE::BSScript::get<int32_t>(*m_variable));
		} else if (m_variable->is<uint32_t>()) {
				variable.type = "uint";
				variable.value = StringFormat("%d", RE::BSScript::get<uint32_t>(*m_variable));
		} else if (m_variable->is<float>()){
				variable.type = "float";
				variable.value = StringFormat("%f", RE::BSScript::get<float>(*m_variable));
		} else if (m_variable->is<bool>()){
				variable.type = "bool";
				variable.value = StringFormat(RE::BSScript::get<bool>(*m_variable) ? "true" : "false");
		}
		#endif
		return true;
	}
}
