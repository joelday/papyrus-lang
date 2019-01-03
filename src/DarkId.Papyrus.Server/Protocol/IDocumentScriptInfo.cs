using DarkId.Papyrus.LanguageService.Compiler;
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
    public class DocumentScriptInfoCapability : DynamicCapability
    {

    }

    public class IdentifierFiles
    {
        public string Identifier { get; set; }
        public Container<string> Files { get; set; }
    }

    public class DocumentScriptInfo
    {
        public Container<string> Identifiers { get; set; }
        public Container<IdentifierFiles> IdentifierFiles { get; set; }
        public Container<string> SearchPaths { get; set; }
    }
    
    public class DocumentScriptInfoParams : ITextDocumentIdentifierParams, IRequest<DocumentScriptInfo>, IBaseRequest
    {
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    [Method("textDocument/scriptInfo")]
    [Parallel]
    public interface IDocumentScriptInfoHandler :
        IJsonRpcRequestHandler<DocumentScriptInfoParams, DocumentScriptInfo>,
        IRequestHandler<DocumentScriptInfoParams, DocumentScriptInfo>,
        IJsonRpcHandler,
        IRegistration<TextDocumentRegistrationOptions>,
        ICapability<DocumentScriptInfoCapability>
    {
    }
}
