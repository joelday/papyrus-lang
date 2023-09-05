#pragma once

#include <string>
#include <dap/io.h>
#include "websocket_impl.h"

namespace dap {

class WebsocketReaderWriter : public ReaderWriter {

  public:
    WebsocketReaderWriter(const std::shared_ptr<connection>& p_con);

    virtual size_t read(void* buffer, size_t n) override;

    virtual bool write(const void* buffer, size_t n) override;

    virtual bool isOpen() override;

    virtual void close() override;
  private:
    void HandleMessage(websocketpp::connection_hdl hdl, message_ptr msg);

    std::shared_ptr<connection> con;
    std::deque<uint8_t> buf;
    bool ready = false;
    std::mutex readMutex;
    std::condition_variable cv;
    std::mutex writeMutex;
    std::atomic<bool> closed = {false};

};
}
