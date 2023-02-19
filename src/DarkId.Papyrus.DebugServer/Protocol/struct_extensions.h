#pragma once

#include <dap/typeof.h>
#include <dap/types.h>
#include <dap/protocol.h>

namespace dap{
  
  // Extended AttachRequest struct for implementation specific parameters

  struct PDSLaunchOrAttach {
        string name;
        string type;
        string request;
        string game;
        optional<string> projectPath;
        optional<string> modDirectory;
        optional<array<Source>> projectSources;
  };

  struct PDSAttachRequest : public AttachRequest {
    using Response = AttachResponse;
    string name;
    string type;
    string request;
    string game;
    optional<string> projectPath;
    optional<string> modDirectory;
    optional<array<Source>> projectSources;
  };

  struct PDSLaunchRequest : public LaunchRequest {
      using Response = LaunchResponse;
      string name;
      string type;
      string request;
      string game;
      optional<string> projectPath;
      optional<string> modDirectory;
      optional<array<Source>> projectSources;
      optional<object> mo2Config;
      optional<string> XSELoaderPath;
      optional<array<string>> args;
  };

  DAP_DECLARE_STRUCT_TYPEINFO(PDSAttachRequest);
  DAP_DECLARE_STRUCT_TYPEINFO(PDSLaunchRequest);

}
