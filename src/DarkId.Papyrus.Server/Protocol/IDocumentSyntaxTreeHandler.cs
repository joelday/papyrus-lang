using DarkId.Papyrus.LanguageService.External;
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
    public class DocumentSyntaxTreeCapability : DynamicCapability
    {
    }

    public class DocumentSyntaxTree
    {
        public DocumentSyntaxTreeNode Root { get; set; }
    }

    public class DocumentSyntaxTreeNode
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public Container<DocumentSyntaxTreeNode> Children { get; set; }
        public Range Range { get; set; }
    }

    public class DocumentSyntaxTreeParams : ITextDocumentIdentifierParams, IRequest<DocumentSyntaxTree>, IBaseRequest
    {
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    [Method("textDocument/syntaxTree")]
    [Parallel]
    public interface IDocumentSyntaxTreeHandler :
        IJsonRpcRequestHandler<DocumentSyntaxTreeParams, DocumentSyntaxTree>,
        IRequestHandler<DocumentSyntaxTreeParams, DocumentSyntaxTree>,
        IJsonRpcHandler,
        IRegistration<TextDocumentRegistrationOptions>,
        ICapability<DocumentSyntaxTreeCapability>
    {
    }
}
