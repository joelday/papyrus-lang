using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server.Protocol
{
    public class DocumentAssemblyCapability : DynamicCapability
    {

    }

    public class DocumentAssembly
    {
        public string Assembly { get; set; }
    }

    public class DocumentAssemblyParams : ITextDocumentIdentifierParams, IRequest<DocumentAssembly>, IBaseRequest
    {
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    [Method("textDocument/assembly")]
    [Parallel]
    public interface IDocumentAssemblyHandler :
        IJsonRpcRequestHandler<DocumentAssemblyParams, DocumentAssembly>,
        IRequestHandler<DocumentAssemblyParams, DocumentAssembly>,
        IJsonRpcHandler,
        IRegistration<TextDocumentRegistrationOptions>,
        ICapability<DocumentAssemblyCapability>
    {
    }
}
