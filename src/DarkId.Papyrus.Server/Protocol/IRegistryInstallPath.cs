using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DarkId.Papyrus.Server.Protocol
{
    public class RegistryInstallPathCapability : DynamicCapability
    {
    }

    public class RegistryInstallPathInfo
    {
        public string Value { get; set; }
        public bool Exists { get; set; }
    }

    public class RegistryInstallPathParams : IRequest<RegistryInstallPathInfo>, IBaseRequest
    {
        public string RegKeyName { get; set; }
    }

    [Method("textDocument/registryInstallPath")]
    [Parallel]
    public interface IRegistryInstallPathHandler :
        IJsonRpcRequestHandler<RegistryInstallPathParams, RegistryInstallPathInfo>,
        IRequestHandler<RegistryInstallPathParams, RegistryInstallPathInfo>,
        IJsonRpcHandler,
        IRegistration<TextDocumentRegistrationOptions>,
        ICapability<RegistryInstallPathCapability>
    {
    }
}
