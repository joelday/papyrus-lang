#include "DebugServerSession.h"

namespace DarkId::Papyrus::DebugServer
{
	DebugServerSession::DebugServerSession(std::function<void(std::string)> sendCallback)
	{
		m_protocol = new VSCodeProtocol(sendCallback);
		m_debugger = new PapyrusDebugger(m_protocol);
		m_protocol->SetDebugger(m_debugger);

		DWORD threadId;
		m_thread = CreateThread(NULL, 0, CommandLoopThreadStart, this, 0, &threadId);
	}

	DebugServerSession::~DebugServerSession()
	{

	}

	void DebugServerSession::Receive(std::string message)
	{
		m_protocol->Receive(message);
	}

	void DebugServerSession::Close()
	{
		m_protocol->Exit();
	}

	DWORD WINAPI DebugServerSession::CommandLoopThreadStart(void* param)
	{
		DebugServerSession* session = (DebugServerSession*)param;
		auto debugger = session->m_debugger;
		auto protocol = session->m_protocol;

		session->m_protocol->CommandLoop();

		delete protocol;
		delete debugger;

		return 0;
	}
}