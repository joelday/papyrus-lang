#pragma once

#include "GameInterfaces.h"

#include "Protocol/protocol.h"

namespace DarkId::Papyrus::DebugServer
{
	class StateNodeBase
	{
		uint32_t m_id = 0;
	public:
		virtual ~StateNodeBase() = default;

		int GetId() const;

		void SetId(uint32_t id);
	};
	
	class RuntimeState;

	class IProtocolVariableSerializable
	{
	public:
		virtual bool SerializeToProtocol(Variable& variable) = 0;
	};

	class IProtocolScopeSerializable
	{
	public:
		virtual bool SerializeToProtocol(Scope& scope) = 0;
	};

	class IStructuredState
	{
	public:
		virtual bool GetChildNames(std::vector<std::string>& names) = 0;
		virtual bool GetChildNode(std::string name, std::shared_ptr<StateNodeBase>& node) = 0;
	};
}
