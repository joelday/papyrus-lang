#pragma once
#include <mutex>

namespace DarkId::Papyrus::DebugServer
{
	class IdProvider
	{
		uint32_t m_currentId = 1000;
		std::mutex m_idMutex;
	public:
		uint32_t GetNext();
	};
}