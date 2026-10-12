#include "websocket_server.h"
#include <thread>
#include <memory>

namespace dap
{
  namespace net
  {

      WebsocketServer::WebsocketServer() : m_server(nullptr), stopped(true), rw(nullptr)
    {
    }

    bool WebsocketServer::start(int p_port,
                        const OnConnect& onConnect,
                        const OnError& onError)
    {
      if (m_server) {
          stop();
      }
      m_server = std::make_unique<server>();
      port = p_port;
      connectCallback = onConnect;
      errorHandler = onError;
      
      stopped = false;
      thread = std::thread(bind(&WebsocketServer::ListenInternal, this));
      return true;
    }

    void WebsocketServer::HandleOpen(websocketpp::connection_hdl hdl)
    {
      std::shared_ptr<connection> con = m_server->get_con_from_hdl(hdl);
      if (!m_connectionHandle.expired() && con != m_server->get_con_from_hdl(m_connectionHandle))
      {
        m_server->close(m_connectionHandle, websocketpp::close::status::normal, "Connection closed by new session.");
      }
      if (rw) {
          rw->close();
      }
      m_connectionHandle = hdl;
      rw = std::make_shared<WebsocketReaderWriter>(con);
      connectCallback(rw);
    }

    void WebsocketServer::HandleClose(websocketpp::connection_hdl hdl)
    {
      if (m_connectionHandle.expired() || m_server->get_con_from_hdl(hdl) != m_server->get_con_from_hdl(m_connectionHandle))
      {
        return;
      }
      if (rw) {
        rw->close();
        rw = nullptr;
      }
    }

    uint32_t WebsocketServer::ListenInternal()
    {
      try
      {
        m_server->init_asio();

        m_server->set_open_handler(bind(&WebsocketServer::HandleOpen, this, ::_1));
        m_server->set_close_handler(bind(&WebsocketServer::HandleClose, this, ::_1));
        m_server->set_max_message_size(1024 * 1024 * 10);
        m_server->set_max_http_body_size(1024 * 1024 * 10);

        m_server->listen(port);
        // Start the server accept loop
        m_server->start_accept();

        // Start the ASIO io_service run loop
        m_server->run();
      }
      catch (websocketpp::exception const & e)
      {
        errorHandler(e.what());
        return e.code().value();
      }
      catch (...)
      {
        errorHandler("other_exception");
        return -1;
      }

      return 0;
    }

    void WebsocketServer::stop()
    {
      std::unique_lock<std::mutex> lock(mutex);
      stopWithLock();
    }

    void WebsocketServer::stopWithLock() {
      if (!stopped) {
          stopped.exchange(true);
        // this will trigger HandleClose for us
        if (m_server && m_server->is_listening()) {
            try 
            {
                m_server->close(m_connectionHandle, websocketpp::close::status::normal, "Connection closed by debugger.");
                m_server->stop();
            }
            catch (websocketpp::exception const& e)
            {
                if (errorHandler) {
                    errorHandler(e.what());
                }
            }
            catch (...)
            {
                if (errorHandler) {
                    errorHandler("other_exception");
                }
            }
        }
        if (thread.joinable()) {
            thread.join();
        }
      }
    }
  }
}