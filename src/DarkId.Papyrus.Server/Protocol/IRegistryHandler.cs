using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DarkId.Papyrus.Server.Protocol
{
    public class RegistryHandlerCapability : DynamicCapability
    {
    }

    public class RegistryHandlerInfo
    {
        public string Value { get; set; }
        public bool Exists { get; set; }
    }

    public class RegistryHandlerParams : IRequest<RegistryHandlerInfo>, IBaseRequest
    {
        public string RegKeyName { get; set; }
    }

    [Method("textDocument/registryHandler")]
    [Parallel]
    public interface IRegistryHandler :
        IJsonRpcRequestHandler<RegistryHandlerParams, RegistryHandlerInfo>,
        IRequestHandler<RegistryHandlerParams, RegistryHandlerInfo>,
        IJsonRpcHandler,
        IRegistration<TextDocumentRegistrationOptions>,
        ICapability<RegistryHandlerCapability>
    {
    }
}
