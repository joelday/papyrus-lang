#pragma once

#include "Protocol/websocket_server.h"
#include "PapyrusDebugger.h"
namespace DarkId::Papyrus::DebugServer
{
	class DebugServer
	{
	public:
		DebugServer();
		void runRestartThread();
		~DebugServer();

		bool Listen();
	private:
		std::unique_ptr<PapyrusDebugger> debugger;
		dap::net::WebsocketServer m_server;
		std::condition_variable cv;
		std::mutex mutex;  // guards 'terminate'
		bool terminate;
		std::thread restart_thread;
	};
}