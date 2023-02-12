#pragma once

#include "Protocol/websocket_server.h"
#include "PapyrusDebugger.h"
namespace DarkId::Papyrus::DebugServer
{
	class DebugServer
	{
	public:
		DebugServer();
		~DebugServer();

		bool Listen();
	private:
		std::unique_ptr<dap::Session> m_session;
		std::unique_ptr<PapyrusDebugger> debugger;
		dap::net::WebsocketServer m_server;
	};
}