#pragma once

#include "Websocket.h"
#include "DebugServerSession.h"

namespace DarkId::Papyrus::DebugServer
{
	class DebugServer
	{
	public:
		DebugServer();
		~DebugServer();

		bool Listen();
	private:
		DebugServerSession* m_session;

		HANDLE m_thread;
		server m_server;

		websocketpp::connection_hdl m_connectionHandle;

		std::basic_streambuf<char>* m_streamBuffer;

		uint32_t ListenInternal();
		
		void Send(std::string message);

		void HandleMessage(websocketpp::connection_hdl hdl, message_ptr msg);
		void HandleOpen(websocketpp::connection_hdl hdl);
		void HandleClose(websocketpp::connection_hdl hdl);

		static DWORD WINAPI ListenThreadStart(void* param);
	};
}