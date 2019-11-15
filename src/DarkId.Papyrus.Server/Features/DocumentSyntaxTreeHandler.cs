// using DarkId.Papyrus.Common;
// using DarkId.Papyrus.LanguageService.Program;
// using DarkId.Papyrus.LanguageService.Syntax;
// using DarkId.Papyrus.Server.Protocol;
// using Microsoft.Extensions.Logging;
// using OmniSharp.Extensions.JsonRpc;
// using OmniSharp.Extensions.LanguageServer.Protocol.Models;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;
// using DarkId.Papyrus.LanguageService.Syntax.Legacy;
// using SyntaxNode = DarkId.Papyrus.LanguageService.Syntax.Legacy.SyntaxNode;

// namespace DarkId.Papyrus.Server.Features
// {
//     [Method("textDocument/syntaxTree")]
//     [Parallel]
//     public class DocumentSyntaxTreeHandler : IDocumentSyntaxTreeHandler
//     {
//         private readonly ProjectManager _projectManager;
//         private readonly ILogger _logger;

//         public static DocumentSyntaxTreeNode GetSyntaxTreeNode(SyntaxNode node)
//         {
//             return new DocumentSyntaxTreeNode()
//             {
//                 Name = node.Kind.ToString(),
//                 Text = node.Text,
//                 Range = node.Range.ToRange(),
//                 Children = node.Children.Select(n => GetSyntaxTreeNode(n)).ToList() ?? new List<DocumentSyntaxTreeNode>()
//             };
//         }

//         public DocumentSyntaxTreeHandler(ProjectManager projectManager, ILogger<DocumentSyntaxTreeHandler> logger)
//         {
//             _projectManager = projectManager;
//             _logger = logger;
//         }

//         public TextDocumentRegistrationOptions GetRegistrationOptions()
//         {
//             return new TextDocumentRegistrationOptions()
//             {
//                 DocumentSelector = Constants.PapyrusScriptSelector
//             };
//         }

//         public Task<DocumentSyntaxTree> Handle(DocumentSyntaxTreeParams request, CancellationToken cancellationToken)
//         {
//             var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
//             if (scriptFile == null)
//             {
//                 return Task.FromResult<DocumentSyntaxTree>(null);
//             }

//             return Task.FromResult(new DocumentSyntaxTree()
//             {
//                 Root = scriptFile.Node != null ? GetSyntaxTreeNode(scriptFile.Node) : null
//             });
//         }

//         public void SetCapability(DocumentSyntaxTreeCapability capability)
//         {
//             capability.DynamicRegistration = true;
//         }
//     }
// }
