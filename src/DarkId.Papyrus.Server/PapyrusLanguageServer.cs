using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.External;
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
        public static Task<ILanguageServer> From(Action<LanguageServerOptions, PapyrusLanguageServerOptions> optionsAction)
        {
            var papyrusOptions = new PapyrusLanguageServerOptions();

            return LanguageServer.From((options) =>
            {
                optionsAction(options, papyrusOptions);

                options.WithServices((collection) => collection
                    .AddSingleton<IFileSystem, LocalFileSystem>()
                    .AddSingleton<IXmlProjectLocator, FileSystemXmlProjectLocator>()
                    .AddSingleton<IXmlProjectDeserializer, XmlProjectDeserializer>()
                    .AddSingleton<IXmlProjectLoader, FileSystemXmlProjectLoader>()
                    .AddSingleton<IScriptTextProvider, TextDocumentScriptTextProvider>((provider) =>
                    {
                        var textProvider = provider.CreateInstance<TextDocumentScriptTextProvider>(
                            provider.CreateInstance<FileSystemScriptTextProvider>());

                        AntlrPatch.SetTextProvider(textProvider);

                        return textProvider;
                    })
                    .AddSingleton<ICreationKitInisLocator>(new CreationKitInisLocator(papyrusOptions.IniLocations))
                    .AddSingleton<ICreationKitConfigLoader, CreationKitInisConfigLoader>()
                    .AddSingleton((provider) =>
                        provider.CreateInstance<CreationKitProgramOptionsProvider>(
                            papyrusOptions.AmbientProjectName,
                            papyrusOptions.FlagsFileName,
                            papyrusOptions.DefaultCreationKitConfig))
                    .AddSingleton<IProgramOptionsProvider>((provider) =>
                        provider.CreateInstance<ProjectProgramOptionsProvider>(papyrusOptions.FlagsFileName))
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
                .WithHandler<DocumentScriptInfoHandler>();

                HarmonyPatches.Apply();
            });
        }
    }
}
