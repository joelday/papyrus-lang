#include "DebugServer.h"
namespace DarkId::Papyrus::DebugServer
{
    DebugServer::DebugServer(): m_session(nullptr){ }

	bool DebugServer::Listen()
	{
  #if SKYRIM
    int port = 43201;
  #elif FALLOUT
    int port = 2077;
  #endif 
	  auto onClientConnected =
      [&](const std::shared_ptr<dap::ReaderWriter>& connection) {
        m_session = dap::Session::create();
        m_session->bind(connection);
        std::shared_ptr<dap::Session> thing = std::move(m_session);
	    debugger = std::unique_ptr<PapyrusDebugger>( new PapyrusDebugger(std::move(thing)) );
        // The Initialize request is the first message sent from the client and
        // the response reports debugger capabilities.
        // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
		m_session->registerHandler([](const dap::InitializeRequest& request) {
			dap::InitializeResponse response;
			response.supportsConfigurationDoneRequest = true;
			response.supportsLoadedSourcesRequest = true;
			return response;
		});

		m_session->registerSentHandler(
			[&](const dap::ResponseOrError<dap::InitializeResponse>&) {
				m_session->send(dap::InitializedEvent());
			});
				
        // Signal used to terminate the server session when a DisconnectRequest
        // is made by the client.
        bool terminate = false;
        std::condition_variable cv;
        std::mutex mutex;  // guards 'terminate'

        // The Disconnect request is made by the client before it disconnects
        // from the server.
        // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Disconnect
        m_session->registerHandler([&](const dap::DisconnectRequest&) {
          // Client wants to disconnect. Set terminate to true, and signal the
          // condition variable to unblock the server thread.
          std::unique_lock<std::mutex> lock(mutex);
          terminate = true;
          cv.notify_one();
          return dap::DisconnectResponse{};
        });

        // Wait for the client to disconnect (or reach a 5 second timeout)
        // before releasing the session and disconnecting the socket to the
        // client.
        std::unique_lock<std::mutex> lock(mutex);
        cv.wait(lock, [&] { return terminate; });
        printf("Session terminated\n");
				m_session = nullptr;
      };
		auto onError = [&](const char* msg) { printf("Server error: %s\n", msg); };

		m_server.start(port, onClientConnected, onError);
		return true;
	}

	DebugServer::~DebugServer()
	{
		m_server.stop();
	}
}