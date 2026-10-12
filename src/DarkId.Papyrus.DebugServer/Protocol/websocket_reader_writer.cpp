#include "pdsPCH.h"
#include "websocket_reader_writer.h"

void dap::WebsocketReaderWriter::HandleMessage(websocketpp::connection_hdl hdl, message_ptr msg) {
    std::unique_lock<std::mutex> lock(readMutex);
    std::string str = "Content-Length: " + std::to_string(msg->get_payload().length()) + "\r\n\r\n" + msg->get_payload();
    for (auto c : str) {
        buf.push_back(c);
    }
    ready = true;
    cv.notify_one();
}

dap::WebsocketReaderWriter::WebsocketReaderWriter(const std::shared_ptr<connection>& p_con): ReaderWriter() {
    con = p_con;
    con->set_message_handler(bind(&WebsocketReaderWriter::HandleMessage, this, ::_1, ::_2));
}

size_t dap::WebsocketReaderWriter::read(void* buffer, size_t n) {
    std::unique_lock<std::mutex> lock(readMutex);
    if (n == 0 || !isOpen()) {
        return 0;
    }
    size_t bytes_read = 0;
    auto out = reinterpret_cast<char*>(buffer);

    while (isOpen() && buf.size() == 0) {
        cv.wait(lock, [&] {return ready; });
    }
    // might have closed while waiting
    if (!isOpen()) {
        return bytes_read;
    }
    for (; bytes_read < n && buf.size() > 0; bytes_read++) {
        out[bytes_read] = buf.front();
        buf.pop_front();
    }
    if (buf.size() == 0)
    {
        ready = false;
    }
    return bytes_read;
}

bool dap::WebsocketReaderWriter::write(const void* buffer, size_t n) {
    std::unique_lock<std::mutex> lock(writeMutex);
    if (!isOpen()) {
        return false;
    }
    std::string str((char*)buffer, n);
    // Header, disregard
    if (str.find("Content-Length") == 0) {
        return true;
    }
    return con->send(str) == websocketpp::lib::error_code();
}

bool dap::WebsocketReaderWriter::isOpen() {
    return con->get_state() == websocketpp::session::state::value::open;
}

void dap::WebsocketReaderWriter::close() {
    std::unique_lock<std::mutex> lock(readMutex);
    std::unique_lock<std::mutex> lock2(writeMutex);
    if (isOpen()) {
        if (con) {
            try {
                con->close(websocketpp::close::status::normal, "Closed by debugger");
            }
            catch (...) {

            }
        }
    }
    ready = true;
    cv.notify_all();
}
