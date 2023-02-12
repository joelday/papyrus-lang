#pragma once

#include <dap/typeof.h>
#include <dap/types.h>
#include <dap/protocol.h>

namespace dap{
  
  // Extended AttachRequest struct for implementation specific parameters
  struct PDSAttachRequest : public AttachRequest {
    using Response = AttachResponse;
    string name;
    string type;
    string request;
    string game;
    optional<string> projectPath;
    optional<string> modDirectory;
    optional<array<string>> sourceFiles;
  };
  DAP_DECLARE_STRUCT_TYPEINFO(PDSAttachRequest);
}
