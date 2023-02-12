#include "DebugServer.h"
#include <thread>
#include <functional>

namespace DarkId::Papyrus::DebugServer
{
    DebugServer::DebugServer() { 
        restart_thread = std::thread(std::bind(&DebugServer::runRestartThread, this));
    }

    void DebugServer::runRestartThread() {
        while (true){
            std::unique_lock<std::mutex> lock(mutex);
            cv.wait(lock, [&] { return terminate; });
            terminate = false;
            debugger = nullptr;
        }
    }

	bool DebugServer::Listen()
	{
  #if SKYRIM
    int port = 43201;
  #elif FALLOUT
    int port = 2077;
  #endif 
	  auto onClientConnected =
      [&](const std::shared_ptr<dap::ReaderWriter>& connection) {
        std::shared_ptr<dap::Session> sess;
        sess = dap::Session::create();
        sess->bind(connection);
        debugger = std::unique_ptr<PapyrusDebugger>( new PapyrusDebugger(sess) );
        sess->registerSentHandler(
            [&](const dap::ResponseOrError<dap::DisconnectResponse>&) {
                terminate = true;
                cv.notify_all();
        });
      };

		auto onError = [&](const char* msg) { 
            logger::error("Server error: %s\n", msg); 
        };

        printf("Session terminated\n");
        auto thread = std::thread();
		m_server.start(port, onClientConnected, onError);
		return true;
	}

	DebugServer::~DebugServer()
	{
		m_server.stop();
        if (restart_thread.joinable()) {
            restart_thread.join();
        }
	}
}