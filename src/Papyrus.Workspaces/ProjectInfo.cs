using System.Collections.Immutable;

namespace Papyrus.Workspaces;

/// <summary>
/// A single script found under a project's source tree (an import or a folder entry).
/// </summary>
public sealed class ProjectInfoScript
{
    public ProjectInfoScript(CodeAnalysis.QualifiedName identifier, string filePath)
    {
        Identifier = identifier;
        FilePath = filePath;
    }

    public CodeAnalysis.QualifiedName Identifier { get; }

    public string FilePath { get; }
}

/// <summary>
/// One source root (an <c>&lt;Import&gt;</c> or <c>&lt;Folder&gt;</c> entry) of a project,
/// along with every script found under it.
/// </summary>
public sealed class ProjectInfoSourceInclude
{
    public ProjectInfoSourceInclude(
        string name,
        string fullPath,
        bool isImport,
        ImmutableArray<ProjectInfoScript> scripts)
    {
        Name = name;
        FullPath = fullPath;
        IsImport = isImport;
        Scripts = scripts.IsDefault ? ImmutableArray<ProjectInfoScript>.Empty : scripts;
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsImport { get; }

    public ImmutableArray<ProjectInfoScript> Scripts { get; }
}

/// <summary>
/// A project's identity and its source layout: every import/folder entry, and every
/// script found under each.
/// </summary>
public sealed class ProjectInfo
{
    public ProjectInfo(string name, ImmutableArray<ProjectInfoSourceInclude> sourceIncludes)
    {
        Name = name;
        SourceIncludes = sourceIncludes.IsDefault
            ? ImmutableArray<ProjectInfoSourceInclude>.Empty
            : sourceIncludes;
    }

    public string Name { get; }

    public ImmutableArray<ProjectInfoSourceInclude> SourceIncludes { get; }
}

/// <summary>
/// Every script file across every loaded project that share one qualified name --
/// more than one file usually means the name is shadowed across import folders.
/// </summary>
public sealed class ScriptIdentifierFiles
{
    public ScriptIdentifierFiles(CodeAnalysis.QualifiedName identifier, ImmutableArray<string> files)
    {
        Identifier = identifier;
        Files = files.IsDefault ? ImmutableArray<string>.Empty : files;
    }

    public CodeAnalysis.QualifiedName Identifier { get; }

    public ImmutableArray<string> Files { get; }
}

/// <summary>
/// The result of resolving a file path to the qualified name(s) it could represent
/// under every loaded project's search paths, plus every file across all projects
/// that shares each candidate name.
/// </summary>
public sealed class ScriptInfo
{
    public ScriptInfo(
        ImmutableArray<CodeAnalysis.QualifiedName> identifiers,
        ImmutableArray<ScriptIdentifierFiles> identifierFiles,
        ImmutableArray<string> searchPaths)
    {
        Identifiers = identifiers.IsDefault
            ? ImmutableArray<CodeAnalysis.QualifiedName>.Empty
            : identifiers;
        IdentifierFiles = identifierFiles.IsDefault
            ? ImmutableArray<ScriptIdentifierFiles>.Empty
            : identifierFiles;
        SearchPaths = searchPaths.IsDefault ? ImmutableArray<string>.Empty : searchPaths;
    }

    public ImmutableArray<CodeAnalysis.QualifiedName> Identifiers { get; }

    public ImmutableArray<ScriptIdentifierFiles> IdentifierFiles { get; }

    public ImmutableArray<string> SearchPaths { get; }
}
