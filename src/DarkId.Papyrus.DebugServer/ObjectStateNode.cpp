#include "ObjectStateNode.h"
#include "Utilities.h"
#include "RuntimeState.h"

#include "FormMetadata.h"
#include "MetaNode.h"



namespace DarkId::Papyrus::DebugServer
{

	ObjectStateNode::ObjectStateNode(const std::string name, RE::BSScript::Object* value, RE::BSScript::ObjectTypeInfo* asClass, const bool subView) :
		m_name(name), m_subView(subView), m_value(value), m_class(asClass)
	{
		if (m_value && !m_subView)
		{
			m_class = RE::BSTSmartPointer<RE::BSScript::ObjectTypeInfo>(m_value->GetTypeInfo());
		}
	}

	bool ObjectStateNode::SerializeToProtocol(Variable& variable)
	{
		variable.variablesReference = m_value ? GetId() : 0;
		
		variable.name = m_name;
		variable.type = m_class->GetName();

		if (m_value)
		{
			std::vector<std::string> childNames;
			GetChildNames(childNames);

			variable.namedVariables = childNames.size();

			if (!m_subView)
			{
				auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();
				const auto handle = vm->GetBoundHandle(m_value);

				variable.value = StringFormat("%s (%08x)", m_class->GetName(), static_cast<uint32_t>(handle ^ 0x0000FFFF00000000));
			}
			else
			{
				variable.value = variable.type;
			}
		}
		else
		{
			variable.value = "None";
		}
		
		return true;
	}

	bool ObjectStateNode::GetChildNames(std::vector<std::string>& names)
	{
		if (!m_value)
		{
			return true;
		}
		
		auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();
		
		FormID formType;
		// TODO: get the type id elsewhere. generic "Form"s and unregistered script forms extended from built-in forms don't register here.
		// May have to get it from the ESP. 
		// Take a look at the getters in TESForms.h, GetFormByEditorID()?
		bool success = vm->GetTypeIDForScriptObject((m_class->name), formType);
		if (success && static_cast<FormType>(formType) < FORM_TYPE_MAX && formType > 0)
		{
			
#if SKYRIM
			auto form = static_cast<RE::TESForm*>(vm->GetObjectHandlePolicy2()->GetObjectForHandle(formType, vm->GetBoundHandle(m_value)));

#define DEFINE_FORM_TYPE_CHECK(type)  \
			if constexpr (meta::isRegistered<##type##>() && !std::is_same<RE::TESForm, ##type##>::value) \
			{\
				auto asType = form->As<##type##>(); \
				if (asType) \
				{ \
					names.push_back(STRING(type)); \
				} \
			} \

			FORM_TYPE_LIST(DEFINE_FORM_TYPE_CHECK)
#undef DEFINE_FORM_TYPE_CHECK

#else
			auto form = static_cast<RE::TESForm*>(vm->GetObjectHandlePolicy().GetObjectForHandle(formType, vm->GetBoundHandle(m_value)));
#define DEFINE_FORM_TYPE_CHECK(type)  \
			if constexpr (meta::isRegistered<##type##>() && !std::is_same<RE::TESForm, ##type##>::value) \
			{ \
				if (static_cast<FormType>(##type##::FORM_ID) == form->GetFormType()) \
				{ \
					auto asType = static_cast<##type##*>(form); \
					if (asType) \
					{ \
						names.push_back(STRING(type)); \
					} \
				} \
			} \

			FORM_TYPE_LIST(DEFINE_FORM_TYPE_CHECK)
#undef DEFINE_FORM_TYPE_CHECK

#endif
			if (names.empty())
			{
				// TESForm is only used as a fallback.
				names.push_back("RE::TESForm");
			}
		}

		if (m_class->GetParent())
		{
			names.push_back("parent");
		}

		const auto variableIter = m_class->GetVariableIter();
		for (auto i = 0; i < m_class->GetNumVariables(); i++)
		{
			auto variable = variableIter[i];
			names.push_back(DemangleName(variable.name.c_str()));
		}

		return true;
	}

	bool ObjectStateNode::GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node)
	{
		auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();

		FormID formType;
		if (m_value && vm->GetTypeIDForScriptObject(m_class->name, formType) && static_cast<FormType>(formType) < FORM_TYPE_MAX && formType > 0)
		{

#if SKYRIM
#define DEFINE_FORM_NODE_RETURN(type)  \
			if (CaseInsensitiveEquals(name, STRING(type))) \
			{\
				auto form = static_cast<##type##*>(vm->GetObjectHandlePolicy2()->GetObjectForHandle(formType, vm->GetBoundHandle(m_value))); \
				node = std::make_shared<MetaNode<##type##*>>(STRING(type), form); \
 				return true; \
			} \

			FORM_TYPE_LIST(DEFINE_FORM_NODE_RETURN)
#undef DEFINE_FORM_NODE_RETURN
#else // FALLOUT
#define DEFINE_FORM_NODE_RETURN(type)  \
			if (CaseInsensitiveEquals(name, STRING(type))) \
			{\
				auto form = static_cast<##type##*>(vm->GetObjectHandlePolicy().GetObjectForHandle(formType, vm->GetBoundHandle(m_value))); \
				node = std::make_shared<MetaNode<##type##*>>(STRING(type), form); \
 				return true; \
			} \

			FORM_TYPE_LIST(DEFINE_FORM_NODE_RETURN)
#undef DEFINE_FORM_NODE_RETURN
#endif

		}
		
		if (m_value && m_class->GetParent() && CaseInsensitiveEquals(name, "parent"))
		{
			node = std::make_shared<ObjectStateNode>("parent", m_value.get(), m_class->GetParent(), true);
			return true;
		}
		
		const auto type = m_value->GetTypeInfo();

		const auto variableIter = type->GetVariableIter();
		for (auto i = 0; i < type->GetNumVariables(); i++)
		{
			const auto variable = &variableIter[i];
			const auto demangledName = DemangleName(variable->name.c_str());

			if (CaseInsensitiveEquals(name, demangledName))
			{
				const auto variableValue = &m_value->variables[i];
				node = RuntimeState::CreateNodeForVariable(demangledName, variableValue);
				return true;
			}
		}

		return false;
	}
}
