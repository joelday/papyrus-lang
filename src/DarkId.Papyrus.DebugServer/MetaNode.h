#pragma once

#include "StateNodeBase.h"

#include <functional>
#include <variant>
#include "FormMetadata.h"
#include "Meta.h"
#include "Utilities.h"

namespace DarkId::Papyrus::DebugServer
{
	class ObjectStateNode;
	
	template<typename T>
	class NullNode :
		public StateNodeBase,
		public IProtocolVariableSerializable
	{
		std::string m_name;
	public:
		NullNode(std::string name) : m_name(name)
		{
		}

		~NullNode() override
		{
		}

		bool SerializeToProtocol(dap::Variable& variable) override
		{
			variable.name = m_name;
			variable.type = std::string(typeid(T).name());
			variable.value = "null";

			return true;
		}
	};
	
	template<typename T>
	class ValueNode :
		public StateNodeBase,
		public IProtocolVariableSerializable
	{
		std::string m_name;
		const T m_value;

	public:
		ValueNode(std::string name, T value) : m_name(name), m_value(value)
		{
		}

		~ValueNode() override
		{
		}

		bool SerializeToProtocol(dap::Variable& variable) override
		{
			variable.name = m_name;
			variable.type = std::string(typeid(T).name());

			if constexpr (std::is_same<T, bool>())
			{
				variable.value = m_value ? "true" : "false";
			}
			#if SKYRIM
			else if constexpr (std::is_same<T, RE::detail::BSFixedString<char>>() || std::is_same<T, RE::detail::BSFixedString<char>>())
			#else
			else if constexpr (std::is_same<T, RE::detail::BSFixedString<char, false>>() || std::is_same<T, RE::detail::BSFixedString<char, true>>())
			#endif
			{
				variable.value = "\"" + std::string(m_value.c_str()) + "\"";
			}
			else if constexpr (std::is_integral<T>())
			{
				variable.value = StringFormat("%d", static_cast<int>(m_value));
			}
			else if constexpr (std::is_floating_point<T>())
			{
				variable.value = StringFormat("%f", static_cast<float>(m_value));
			}
			else if constexpr (std::is_convertible<T, std::string>())
			{
				variable.value = "\"" + std::string(m_value) + "\"";
			}
			else
			{
				variable.value = meta::toDisplayValue<T>(m_value);
			}

			return true;
		}
	};
	
	template<class Class>
	class MetaNode :
		public StateNodeBase,
		public IProtocolVariableSerializable,
		public IStructuredState
	{
		using NonPtrClass = typename std::remove_pointer<Class>::type;
		static const bool isPointer = std::is_pointer<Class>();
		
		std::string m_name;
		Class m_value;
		
	public:
		MetaNode(std::string name, Class value) : m_name(name), m_value(value)
		{
		}
		
		~MetaNode() override
		{
		}

		bool SerializeToProtocol(dap::Variable& variable) override
		{
			variable.variablesReference = GetId();
			variable.namedVariables = meta::getMemberCount<NonPtrClass>();
			variable.name = m_name;
			variable.value = meta::toDisplayValue<Class>(m_value);

			return true;
		}

		bool GetChildNames(std::vector<std::string>& names) override
		{
			meta::doForAllMembers<NonPtrClass>([&names](auto& member)
			{
				names.push_back(member.getName());
			});
			
			return true;
		}

		NonPtrClass& GetValue()
		{
			if constexpr (isPointer)
			{
				return *m_value;
			}
			else
			{
				return m_value;
			}
		}

		bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) override
		{
			auto found = false;

			meta::doForAllMembers<NonPtrClass>([this, &found, &name, &node](auto& member)
			{
				auto memberName = std::string(member.getName());
				
				if (!CaseInsensitiveEquals(name, memberName))
				{
					return;
				}

				found = true;

				using TValue = meta::get_member_type<decltype(member)>;
				TValue memberValue = member.getCopy(GetValue());

				if (std::is_pointer<TValue>::value && XSE::stl::unrestricted_cast<void*>(memberValue) == nullptr)
				{
					node = std::make_shared<NullNode<TValue>>(memberName);
				}
				// TODO: check if this should be checking if it's the same as RE::BSScript::BSSmartPointer<RE::BSScript::Object*>
				else if constexpr (std::is_same<TValue, RE::BSScript::Object*>::value)
				{
					RE::BSScript::Object * obj = static_cast<RE::BSScript::Object*>(memberValue);
					RE::BSScript::ObjectTypeInfo * type_info = obj->GetTypeInfo();
					node = std::make_shared<ObjectStateNode>(memberName, obj, type_info, false);
				}
				else if constexpr (meta::isRegistered<typename std::remove_pointer<TValue>::type>())
				{
					node = std::make_shared<MetaNode<TValue>>(memberName, memberValue);
				}
				else
				{
					node = std::make_shared<ValueNode<TValue>>(memberName, memberValue);
				}
			});

			return found;
		}
	};
}
