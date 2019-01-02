using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace DarkId.Papyrus.Server
{
    public class TextDocumentScriptTextProvider :
        IScriptTextProvider,
        IDidChangeTextDocumentHandler,
        IDidOpenTextDocumentHandler,
        IDidCloseTextDocumentHandler
    {
        private readonly object _lock = new object();

        private readonly IScriptTextProvider _baseProvider;

        private readonly Dictionary<string, ScriptText> _documentItems
            = new Dictionary<string, ScriptText>(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger _logger;

        public TextDocumentScriptTextProvider(IScriptTextProvider baseProvider, ILogger<TextDocumentScriptTextProvider> logger)
        {
            _logger = logger;
            _baseProvider = baseProvider;
        }

        public TextDocumentSyncKind Change => TextDocumentSyncKind.Incremental;

        public event EventHandler<ScriptTextChangedEventArgs> OnScriptTextChanged;
        public event EventHandler<DidOpenTextDocumentParams> OnDidOpenTextDocument;

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                SyncKind = TextDocumentSyncKind.Incremental,
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        private void RaiseOnScriptTextChanged(ScriptText scriptText)
        {
            _logger.LogDebug($"Script text changed: {scriptText.FilePath}");

            OnScriptTextChanged?.Invoke(this, new ScriptTextChangedEventArgs(scriptText));
        }

        public async Task<ScriptText> GetText(string filePath)
        {
            lock (_lock)
            {
                if (_documentItems.ContainsKey(filePath))
                {
                    return _documentItems[filePath];
                }
            }

            return await _baseProvider.GetText(filePath);
        }

        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            var filePath = uri.ToFilePath();
            var extension = Path.GetExtension(filePath).ToLower();

            return new TextDocumentAttributes(uri,
                "file", extension == ".psc" ? "papyrus" :
                    extension == ".ppj" ? "papyrus-project" : null);
        }

        public async Task<string> GetTextVersion(string filePath)
        {
            lock (_lock)
            {
                if (_documentItems.ContainsKey(filePath))
                {
                    return _documentItems[filePath].Version.ToString();
                }
            }

            return await _baseProvider.GetTextVersion(filePath);
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                var scriptText = _documentItems[request.TextDocument.Uri.ToFilePath()];

                scriptText.Update(
                    request.TextDocument.Version.ToString(),
                    request.ContentChanges.Select(change => change.ToScriptTextChange()).ToArray()
                );

                RaiseOnScriptTextChanged(scriptText);
            }

            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            OnDidOpenTextDocument?.Invoke(this, request);

            lock (_lock)
            {
                var filePath = request.TextDocument.Uri.ToFilePath();

                _documentItems.Add(
                    filePath,
                    new ScriptText(filePath,
                        request.TextDocument.Text,
                        request.TextDocument.Version.ToString()
                    ));
            }

            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                var filePath = request.TextDocument.Uri.ToFilePath();

                _documentItems.Remove(filePath);
            }

            return Unit.Task;
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            capability.DidSave = false;
            capability.DynamicRegistration = true;
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return GetRegistrationOptions();
        }
    }
}