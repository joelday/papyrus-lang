using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Projects;
using DynamicData;
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
        private readonly IScriptTextProvider _baseProvider;

        private SourceCache<ScriptText, StringOrdinalIgnore> _documentItems = new SourceCache<ScriptText, StringOrdinalIgnore>(o => o.FilePath);
        private readonly ILogger _logger;

        public TextDocumentScriptTextProvider(IScriptTextProvider baseProvider, ILogger<TextDocumentScriptTextProvider> logger)
        {
            _logger = logger;
            _baseProvider = baseProvider;
        }

        public TextDocumentSyncKind Change => TextDocumentSyncKind.Incremental;
        private readonly Subject<TextDocumentItem> _onDidOpenTextDocument = new Subject<TextDocumentItem>();
        public IObservable<TextDocumentItem> OnDidOpenTextDocument => _onDidOpenTextDocument;

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                SyncKind = TextDocumentSyncKind.Incremental,
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        public async Task<ScriptText> GetText(string filePath)
        {
            if (_documentItems.TryGetValue(filePath, out var scriptText))
            {
                return scriptText;
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
            if (_documentItems.TryGetValue(filePath, out var scriptText))
            {
                return scriptText.Version.ToString();
            }

            return await _baseProvider.GetTextVersion(filePath);
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var path = request.TextDocument.Uri.ToFilePath();
            if (!_documentItems.TryGetValue(request.TextDocument.Uri.ToFilePath(), out var scriptText))
            {
                throw new ArgumentException($"Script text was updated for a document that was not registered: {path}");
            }

            scriptText.Update(
                request.TextDocument.Version.ToString(),
                request.ContentChanges.Select(change => change.ToScriptTextChange()).ToArray()
            );

            _logger.LogDebug($"Script text changed: {scriptText.FilePath}");
            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            _onDidOpenTextDocument.OnNext(request.TextDocument);
            _documentItems.AddOrUpdate(
                new ScriptText(
                    request.TextDocument.Uri.ToFilePath(),
                    request.TextDocument.Text,
                    request.TextDocument.Version.ToString()));
            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            _documentItems.Remove(request.TextDocument.Uri.ToFilePath());
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

        public IObservable<System.Reactive.Unit> ScriptTextChanged(string filePath)
        {
            return _documentItems.Connect()
                .Watch(filePath)
                .Select(s => s.Current?.Text ?? Observable.Empty<string>())
                .Switch()
                .Unit()
                .Publish()
                .RefCount();
        }
    }
}