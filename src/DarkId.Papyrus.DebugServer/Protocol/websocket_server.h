#pragma once

#include <functional>
#include <memory>
#include <dap/network.h>
#include "websocket_impl.h"
#include "websocket_reader_writer.h"

namespace dap {

namespace net {
  class WebsocketServer: 
    public Server
  {
  // ignoreErrors() matches the OnError signature, and does nothing.
  static inline void ignoreErrors(const char*) {}
	uint32_t ListenInternal();
  void stopWithLock();
  void HandleOpen(websocketpp::connection_hdl hdl);
  void HandleClose(websocketpp::connection_hdl hdl);

  int port = 0;
  std::mutex mutex;
  std::thread thread;
  std::atomic<bool> stopped;
  std::unique_ptr<WebsocketReaderWriter> rw;
  OnConnect connectCallback;
  OnError errorHandler;
  server m_server;
  websocketpp::connection_hdl m_connectionHandle;
public:
  using OnError = std::function<void(const char*)>;
  using OnConnect = std::function<void(const std::shared_ptr<ReaderWriter>&)>;

  WebsocketServer(){};
  virtual ~WebsocketServer() { stop(); };

  // start() begins listening for connections on the given port.
  // callback will be called for each connection.
  // onError will be called for any connection errors.
  virtual bool start(int p_port,
                     const OnConnect& callback,
                     const OnError& onError = ignoreErrors) override;

  // stop() stops listening for connections.
  // stop() is implicitly called on destruction.
  virtual void stop() override;  
};

}
}