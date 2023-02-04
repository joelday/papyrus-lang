#include "StateNodeBase.h"
namespace DarkId::Papyrus::DebugServer
{
	int StateNodeBase::GetId() const
	{
		return m_id;
	}

	void StateNodeBase::SetId(const uint32_t id)
	{
		m_id = id;
	}
}

