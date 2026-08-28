using System;
using System.Threading;
using System.Threading.Tasks;

namespace Papyrus.LanguageServer;

/// <summary>
/// Resolves a file path to the qualified name(s) it could represent, plus shadowing
/// info across every loaded project's search paths. Reimplements the legacy
/// <c>textDocument/scriptInfo</c> handler against <see cref="Workspaces.PapyrusSolution"/>.
/// </summary>
internal sealed class DocumentScriptInfoHandler
    : ILspHandler<DocumentScriptInfoParams?, DocumentScriptInfo>
{
    internal const string MethodName = "textDocument/scriptInfo";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<DocumentScriptInfo> HandleAsync(
        DocumentScriptInfoParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (!LspUris.TryGetFilePath(p?.TextDocument?.Uri, out var path))
        {
            return Task.FromResult(new DocumentScriptInfo());
        }

        var info = snapshot.Solution.GetScriptInfo(path);
        var identifierFiles = new IdentifierFiles[info.IdentifierFiles.Length];
        for (var i = 0; i < identifierFiles.Length; i++)
        {
            var entry = info.IdentifierFiles[i];
            identifierFiles[i] = new IdentifierFiles
            {
                Identifier = entry.Identifier.ToString(),
                Files = [.. entry.Files],
            };
        }

        var identifiers = new string[info.Identifiers.Length];
        for (var i = 0; i < identifiers.Length; i++)
        {
            identifiers[i] = info.Identifiers[i].ToString();
        }

        return Task.FromResult(
            new DocumentScriptInfo
            {
                Identifiers = identifiers,
                IdentifierFiles = identifierFiles,
                SearchPaths = [.. info.SearchPaths],
            }
        );
    }
}

/// <summary>
/// Describes every loaded project's name and source layout. Reimplements the
/// legacy <c>papyrus/projectInfos</c> handler against
/// <see cref="Workspaces.PapyrusSolution"/>.
/// </summary>
internal sealed class ProjectInfosHandler : ILspHandler<ProjectInfosParams?, ProjectInfos>
{
    internal const string MethodName = "papyrus/projectInfos";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<ProjectInfos> HandleAsync(
        ProjectInfosParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var infos = snapshot.Solution.GetProjectInfos();
        var projects = new ProjectInfo[infos.Length];
        for (var i = 0; i < infos.Length; i++)
        {
            projects[i] = ToLsp(infos[i]);
        }

        return Task.FromResult(new ProjectInfos { Projects = projects });
    }

    private static ProjectInfo ToLsp(Workspaces.ProjectInfo info)
    {
        var includes = new ProjectInfoSourceInclude[info.SourceIncludes.Length];
        for (var i = 0; i < includes.Length; i++)
        {
            var include = info.SourceIncludes[i];
            var scripts = new ProjectInfoScript[include.Scripts.Length];
            for (var j = 0; j < scripts.Length; j++)
            {
                var script = include.Scripts[j];
                scripts[j] = new ProjectInfoScript
                {
                    Identifier = script.Identifier.ToString(),
                    FilePath = script.FilePath,
                };
            }

            includes[i] = new ProjectInfoSourceInclude
            {
                Name = include.Name,
                FullPath = include.FullPath,
                IsImport = include.IsImport,
                Scripts = scripts,
            };
        }

        return new ProjectInfo { Name = info.Name, SourceIncludes = includes };
    }
}
