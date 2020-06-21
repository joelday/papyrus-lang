using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.Server.Protocol;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/scriptInfo")]
    [Parallel]
    public class DocumentScriptInfoHandler : IDocumentScriptInfoHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public DocumentScriptInfoHandler(ProjectManager projectManager, ILogger<DocumentScriptInfoHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        public async Task<DocumentScriptInfo> Handle(DocumentScriptInfoParams request, CancellationToken cancellationToken)
        {
            try
            {
                var filePath = request.TextDocument.Uri.ToFilePath();
                var sourceIncludes = _projectManager.Projects.Items.SelectMany(p => p.Program.Options.Sources.Includes);

                var searchPaths = sourceIncludes.Select(include => include.Path).Distinct().ToArray();

                var possibleImportPaths = searchPaths.Where(path => filePath.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToArray();
                var possibleIdentifiers = possibleImportPaths.Select(path =>
                    ObjectIdentifier.FromScriptFilePath(PathUtilities.PathNetCore.GetRelativePath(path, filePath)).ToString()).ToArray();

                var scriptFiles = possibleIdentifiers.SelectMany(identifier =>
                    _projectManager.Projects.Items.Select(p => p.Program.ScriptFiles.GetValueOrDefault(identifier))).WhereNotNull().ToArray();

                return new DocumentScriptInfo()
                {
                    Identifiers = possibleIdentifiers,
                    IdentifierFiles = scriptFiles.GroupBy(f => f.Id).ToDictionary(k => k.Key.ToString(), k => k.Select(f => f.FilePath).ToList()).ToList().Select(kvp => new IdentifierFiles()
                    {
                        Identifier = kvp.Key,
                        Files = kvp.Value
                    }).ToArray(),
                    SearchPaths = searchPaths
                };
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return null;
        }

        public void SetCapability(DocumentScriptInfoCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
