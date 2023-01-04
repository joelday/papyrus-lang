#include "DebugServer.h"
namespace DarkId::Papyrus::DebugServer
{
	DebugServer::DebugServer() :
		m_session(NULL)
	{

	}

	void DebugServer::Send(std::string message)
	{
		m_server.send(m_connectionHandle, message.c_str(), message.length(), websocketpp::frame::opcode::text);
	}

	void DebugServer::HandleMessage(websocketpp::connection_hdl hdl, message_ptr msg)
	{
		if (m_session)
		{
			m_session->Receive(msg->get_payload());
		}
	}

	void DebugServer::HandleOpen(websocketpp::connection_hdl hdl)
	{
		if (m_session && m_server.get_con_from_hdl(hdl) != m_server.get_con_from_hdl(m_connectionHandle))
		{
			m_server.close(m_connectionHandle, websocketpp::close::status::normal, "Connection closed by new session.");
		}

		m_connectionHandle = hdl;
		m_session = new DebugServerSession(std::bind(&DebugServer::Send, this, ::_1));
	}

	void DebugServer::HandleClose(websocketpp::connection_hdl hdl)
	{
		if (m_server.get_con_from_hdl(hdl) != m_server.get_con_from_hdl(m_connectionHandle))
		{
			return;
		}

		m_session->Close();
		delete m_session;
		m_session = NULL;
	}

	uint32_t DebugServer::ListenInternal()
	{
		try
		{
			m_server.init_asio();

			m_server.set_open_handler(bind(&DebugServer::HandleOpen, this, ::_1));
			m_server.set_close_handler(bind(&DebugServer::HandleClose, this, ::_1));
			m_server.set_message_handler(bind(&DebugServer::HandleMessage, this, ::_1, ::_2));
			m_server.set_max_message_size(1024 * 1024 * 10);
			m_server.set_max_http_body_size(1024 * 1024 * 10);

			// Listen on port 43201
			// TODO: Make configurable

#if SKYRIM
			m_server.listen(43201);
#elif FALLOUT
			m_server.listen(2077);
#endif 

			// Start the server accept loop
			m_server.start_accept();

			// Start the ASIO io_service run loop
			m_server.run();
		}
		catch (websocketpp::exception const & e)
		{
			logger::info("{}"sv, e.what());
			return e.code().value();
		}
		catch (...)
		{
			logger::info("other_exception");
			return -1;
		}

		return 0;
	}

	DWORD DebugServer::ListenThreadStart(void* param)
	{
		DebugServer* server = (DebugServer*)param;
		return server->ListenInternal();
	}

	bool DebugServer::Listen()
	{
		DWORD threadId;

		m_thread = CreateThread(NULL, 0, ListenThreadStart, this, 0, &threadId);

		return true;
	}

	DebugServer::~DebugServer()
	{
		m_server.stop();
	}
}