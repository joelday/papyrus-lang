using DarkId.Papyrus.LanguageService.Common;
using DarkId.Papyrus.LanguageService.Compiler;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Projects;
using DarkId.Papyrus.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server
{
    public static class PapyrusLanguageServer
    {
        public static Task<ILanguageServer> From(Action<LanguageServerOptions> optionsAction)
        {
            return LanguageServer.From((options) =>
            {
                optionsAction(options);

                options.WithServices((collection) => collection
                    .AddSingleton<IFileSystem, LocalFileSystem>()
                    .AddSingleton<IXmlProjectLocator, FileSystemXmlProjectLocator>()
                    .AddSingleton<IXmlProjectDeserializer, XmlProjectDeserializer>()
                    .AddSingleton<IXmlProjectLoader, FileSystemXmlProjectLoader>()
                    .AddSingleton<IScriptTextProvider, TextDocumentScriptTextProvider>((provider) =>
                    {
                        var textProvider = new TextDocumentScriptTextProvider(
                            provider.CreateInstance<FileSystemScriptTextProvider>(),
                            provider.GetService<ILogger<TextDocumentScriptTextProvider>>());

                        AntlrPatch.SetTextProvider(textProvider);

                        return textProvider;
                    })
                    .AddSingleton<ICreationKitInisLocator, CreationKitInisLocator>()
                    .AddSingleton<ICreationKitConfigLoader, CreationKitInisConfigLoader>()
                    .AddSingleton<CreationKitProgramOptionsProvider>()
                    .AddSingleton<IProgramOptionsProvider, ProjectProgramOptionsProvider>()
                    .AddSingleton<ProjectManager>())
                .WithHandler<WorkspaceManager>()
                .WithHandler<DefinitionHandler>()
                .WithHandler<DocumentSymbolHandler>()
                .WithHandler<DocumentSyntaxTreeHandler>()
                .WithHandler<HoverHandler>()
                .WithHandler<CompletionHandler>()
                .WithHandler<SignatureHelpHandler>()
                .WithHandler<ReferencesHandler>()
                .WithHandler<RenameHandler>()
                .WithHandler<DocumentScriptInfoHandler>()
                .WithHandler<RegistryHandler>();

                HarmonyPatches.Apply();
            });
        }
    }
}
