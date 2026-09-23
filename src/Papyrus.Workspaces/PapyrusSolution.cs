using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.Workspaces;

public sealed class PapyrusSolution
{
    public static PapyrusSolution Empty { get; } = new(
        ImmutableDictionary<ProjectId, ProjectState>.Empty,
        ImmutableDictionary<DocumentId, SourceText>.Empty);

    private readonly ImmutableDictionary<ProjectId, ProjectState> _projects;
    private readonly ImmutableDictionary<DocumentId, SourceText> _openDocuments;

    private PapyrusSolution(
        ImmutableDictionary<ProjectId, ProjectState> projects,
        ImmutableDictionary<DocumentId, SourceText> openDocuments)
    {
        _projects = projects;
        _openDocuments = openDocuments;

        var projectIds = ImmutableArray.CreateBuilder<ProjectId>(projects.Count);
        foreach (var pair in projects)
        {
            projectIds.Add(pair.Key);
        }

        ProjectIds = projectIds.ToImmutable();

        var documentIds = ImmutableArray.CreateBuilder<DocumentId>(openDocuments.Count);
        foreach (var pair in openDocuments)
        {
            documentIds.Add(pair.Key);
        }

        OpenDocumentIds = documentIds.ToImmutable();
    }

    public ImmutableArray<ProjectId> ProjectIds { get; }

    public ImmutableArray<DocumentId> OpenDocumentIds { get; }

    public IEnumerable<PapyrusProject> Projects
    {
        get
        {
            foreach (var pair in _projects)
            {
                yield return pair.Value.Project;
            }
        }
    }

    /// <summary>
    /// Describes every loaded project's name and source layout (each import/folder
    /// entry plus the scripts found under it). Mirrors the legacy
    /// <c>papyrus/projectInfos</c> shape.
    /// </summary>
    public ImmutableArray<ProjectInfo> GetProjectInfos()
    {
        var builder = ImmutableArray.CreateBuilder<ProjectInfo>(_projects.Count);
        foreach (var pair in _projects)
        {
            builder.Add(DescribeProject(pair.Value));
        }

        return builder.MoveToImmutable();
    }

    private static ProjectInfo DescribeProject(ProjectState state)
    {
        var project = state.Project;
        var resolver = state.DiskResolver;
        var name = project.Path is null
            ? project.Game?.ToString() ?? "Unknown"
            : Path.GetFileNameWithoutExtension(project.Path);

        // IncludeDirectories is built import-then-folder (see
        // FileSystemScriptResolver(PapyrusProject, string)), so the same index maps
        // each resolved directory back to the import/folder entry that produced it.
        var includes = ImmutableArray.CreateBuilder<ProjectInfoSourceInclude>(
            resolver.IncludeDirectories.Length);
        for (var i = 0; i < resolver.IncludeDirectories.Length; i++)
        {
            var isImport = i < project.Imports.Length;
            var declared = isImport
                ? project.Imports[i]
                : project.Folders[i - project.Imports.Length].Path;
            includes.Add(
                new ProjectInfoSourceInclude(
                    declared,
                    resolver.IncludeDirectories[i],
                    isImport,
                    ScriptsUnder(resolver.IncludeDirectories[i], resolver)));
        }

        return new ProjectInfo(name, includes.MoveToImmutable());
    }

    private static ImmutableArray<ProjectInfoScript> ScriptsUnder(
        string directory,
        FileSystemScriptResolver resolver)
    {
        if (!Directory.Exists(directory))
        {
            return ImmutableArray<ProjectInfoScript>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ProjectInfoScript>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.psc", SearchOption.AllDirectories))
        {
            if (resolver.TryGetNameFromPath(path, out var name))
            {
                builder.Add(new ProjectInfoScript(name, path));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Resolves a file path against every loaded project's search paths, returning
    /// every qualified name the path could represent plus, per candidate name, every
    /// file (across every project) that shares it -- more than one file for a name
    /// usually means it is shadowed across import folders. Mirrors the legacy
    /// <c>textDocument/scriptInfo</c> shape.
    /// </summary>
    public ScriptInfo GetScriptInfo(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var full = Path.GetFullPath(filePath);

        var searchPaths = ImmutableArray.CreateBuilder<string>();
        var seenSearchPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _projects)
        {
            foreach (var directory in pair.Value.DiskResolver.IncludeDirectories)
            {
                if (seenSearchPaths.Add(directory))
                {
                    searchPaths.Add(directory);
                }
            }
        }

        var identifiers = ImmutableArray.CreateBuilder<CodeAnalysis.QualifiedName>();
        var seenIdentifiers = new HashSet<CodeAnalysis.QualifiedName>();
        foreach (var pair in _projects)
        {
            if (pair.Value.DiskResolver.TryGetNameFromPath(full, out var name)
                && seenIdentifiers.Add(name))
            {
                identifiers.Add(name);
            }
        }

        var identifierFiles = ImmutableArray.CreateBuilder<ScriptIdentifierFiles>(identifiers.Count);
        foreach (var identifier in identifiers)
        {
            identifierFiles.Add(new ScriptIdentifierFiles(identifier, FilesForIdentifier(identifier)));
        }

        return new ScriptInfo(
            identifiers.ToImmutable(),
            identifierFiles.MoveToImmutable(),
            searchPaths.ToImmutable());
    }

    private ImmutableArray<string> FilesForIdentifier(CodeAnalysis.QualifiedName identifier)
    {
        var files = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _projects)
        {
            foreach (var candidate in pair.Value.DiskResolver.CandidateSourcePaths(identifier))
            {
                if (File.Exists(candidate) && seen.Add(candidate))
                {
                    files.Add(candidate);
                }
            }
        }

        return files.ToImmutable();
    }

    /// <summary>
    /// Disassembles a document's compiled script into Caprica/CK-flavor Papyrus
    /// assembly (.pas) text, forcing that script to be emitted so the result
    /// reflects the current buffer. Returns null for documents that cannot be
    /// resolved, or that fail to bind/emit (diagnostics should already explain why
    /// via <see cref="GetDiagnostics(DocumentId, CancellationToken)"/>). Flags files
    /// are not consulted here -- this is a read-only diagnostic view of the script,
    /// not a build artifact, so user-flag names in the output may differ slightly
    /// from a real build's PEX.
    /// </summary>
    public string? GetAssembly(DocumentId documentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_projects.TryGetValue(documentId.ProjectId, out var state)
            || !state.DiskResolver.TryGetNameFromPath(documentId.Path, out var name))
        {
            return null;
        }

        try
        {
            // Emit diagnostics alone miss binding errors (e.g. an unresolved type):
            // the emitter can still walk a partially-bound tree without complaint.
            // Check the script's own binding diagnostics first, same as the
            // compiler CLI does before writing a PEX.
            var diagnostics = state.Compilation.GetDiagnostics(name, cancellationToken);
            if (HasError(diagnostics))
            {
                return null;
            }

            var script = state.Compilation.GetScript(name);
            var emitter = new CodeAnalysis.Emit.PexEmitter(state.Flags);
            var pex = emitter.EmitFile(script, cancellationToken);
            return HasError(emitter.Diagnostics)
                ? null
                : global::Papyrus.Pex.PasDisassembler.Disassemble(pex);
        }
        catch (Exception ex)
            when (ex is InvalidOperationException or NotSupportedException
                or global::Papyrus.Pex.PexFormatException)
        {
            return null;
        }
    }

    private static bool HasError(ImmutableArray<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    public PapyrusCompilation? Compilation
    {
        get
        {
            foreach (var pair in _projects)
            {
                return pair.Value.Compilation;
            }

            return null;
        }
    }

    public bool ContainsProject(ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        return _projects.ContainsKey(projectId);
    }

    public PapyrusProject GetProject(ProjectId projectId)
    {
        return GetState(projectId).Project;
    }

    public bool TryGetProject(ProjectId projectId, out PapyrusProject project)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        if (_projects.TryGetValue(projectId, out var state))
        {
            project = state.Project;
            return true;
        }

        project = null!;
        return false;
    }

    public PapyrusCompilation GetCompilation(ProjectId projectId)
    {
        return GetState(projectId).Compilation;
    }

    /// <summary>
    /// Finds every reference to <paramref name="symbol"/> across every script in
    /// the given project, including scripts that have not been opened as
    /// documents. Open-document edits are already reflected in the project's
    /// <see cref="PapyrusCompilation"/>, so this picks them up automatically;
    /// only new, unsaved files with no path on disk are not covered. Symbols
    /// are compared by reference and are only meaningful within the
    /// compilation they were resolved from, so searching is scoped to a
    /// single project.
    /// </summary>
    public ImmutableArray<ScriptReferences> FindReferences(
        ProjectId projectId,
        CodeAnalysis.Symbols.ISymbol symbol,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var state = GetState(projectId);

        var results = ImmutableArray.CreateBuilder<ScriptReferences>();
        var declaringScript = (symbol as CodeAnalysis.Symbols.IScriptSymbol ?? symbol.ContainingScript)?.QualifiedName;
        foreach (var (name, path) in state.DiskResolver.GetScripts())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tree = state.Compilation.GetSyntaxTree(name);

            // Binding is the expensive part: skip scripts that never mention the name.
            if (!name.Equals(declaringScript)
                && !CodeAnalysis.Structure.ScriptIndex.Get(tree).MentionsName(symbol.Name.Text))
            {
                continue;
            }

            var model = state.Compilation.GetSemanticModel(tree);
            var spans = model.FindReferencesInTree(symbol, cancellationToken);
            if (!spans.IsEmpty)
            {
                results.Add(new ScriptReferences(path, tree, spans));
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// The project's own scripts: those its Folders and Scripts name, as the compiler
    /// builds them, not the Imports it only compiles against.
    /// </summary>
    public ImmutableArray<DocumentId> GetSourceDocumentIds(ProjectId projectId)
    {
        var state = GetState(projectId);
        var project = state.Project;
        var directory = System.IO.Path.GetDirectoryName(project.Path) ?? "";
        var folders = new List<(string Path, bool NoRecurse)>();
        foreach (var folder in project.Folders)
        {
            if (!string.IsNullOrWhiteSpace(folder.Path))
            {
                folders.Add((WorkspacePaths.NormalizeFull(System.IO.Path.Combine(directory, folder.Path)).TrimEnd('/'), folder.NoRecurse));
            }
        }

        var scripts = new List<string>();
        foreach (var script in project.Scripts)
        {
            if (!string.IsNullOrWhiteSpace(script))
            {
                scripts.Add(script.Replace('\\', '/').TrimStart('/'));
            }
        }

        var builder = ImmutableArray.CreateBuilder<DocumentId>();
        foreach (var (_, path) in state.DiskResolver.GetScripts())
        {
            if (IsProjectSource(path, folders, scripts))
            {
                builder.Add(DocumentId.Create(projectId, path));
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsProjectSource(string path, List<(string Path, bool NoRecurse)> folders, List<string> scripts)
    {
        // Normalized paths use '/' on every platform.
        var full = WorkspacePaths.NormalizeFull(path);
        var parent = full[..Math.Max(0, full.LastIndexOf('/'))];
        foreach (var (folder, noRecurse) in folders)
        {
            if (noRecurse
                ? string.Equals(parent, folder, StringComparison.OrdinalIgnoreCase)
                : full.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var script in scripts)
        {
            if (full.EndsWith("/" + script, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The name of every script in a project's search paths.</summary>
    public ImmutableArray<QualifiedName> GetScriptNames(ProjectId projectId)
    {
        var scripts = GetState(projectId).DiskResolver.GetScripts();
        var builder = ImmutableArray.CreateBuilder<QualifiedName>(scripts.Length);
        foreach (var (name, _) in scripts)
        {
            builder.Add(name);
        }

        return builder.MoveToImmutable();
    }

    /// <summary>The source file of script <paramref name="name"/> in a project's search paths.</summary>
    public bool TryGetScriptPath(ProjectId projectId, QualifiedName name, out string path)
    {
        return GetState(projectId).DiskResolver.TryGetSourcePath(name, out path);
    }

    /// <summary>
    /// Scripts in the project that extend <paramref name="script"/> (directly, or at any
    /// depth when <paramref name="transitive"/>). Each script's Extends name (from its
    /// syntax index) prefilters, and only candidates are resolved.
    /// </summary>
    public ImmutableArray<(QualifiedName Name, string Path)> FindDerivedScripts(
        ProjectId projectId,
        CodeAnalysis.Symbols.IScriptSymbol script,
        bool transitive,
        CancellationToken cancellationToken = default)
    {
        var state = GetState(projectId);
        var targets = new HashSet<QualifiedName> { script.QualifiedName };
        var targetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { script.Name.Text };
        var results = ImmutableArray.CreateBuilder<(QualifiedName, string)>();
        var found = true;
        while (found)
        {
            found = false;
            foreach (var (name, path) in state.DiskResolver.GetScripts())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (targets.Contains(name))
                {
                    continue;
                }

                var baseName = CodeAnalysis.Structure.ScriptIndex.Get(state.Compilation.GetSyntaxTree(name)).BaseName;
                if (baseName is null || !targetNames.Contains(baseName[(baseName.LastIndexOf(':') + 1)..]))
                {
                    continue;
                }

                if (state.Compilation.GetBaseScript(name) is { } resolved && targets.Contains(resolved.QualifiedName))
                {
                    results.Add((name, path));
                    if (transitive)
                    {
                        targets.Add(name);
                        targetNames.Add(name.Parts[^1].Text);
                        found = true;
                    }
                }
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Where <paramref name="symbol"/> is implemented further down the hierarchy: for a
    /// script, every script extending it; for a function or event, the same-named
    /// declarations in those scripts.
    /// </summary>
    public ImmutableArray<DeclarationMatch> FindImplementations(
        ProjectId projectId,
        CodeAnalysis.Symbols.ISymbol symbol,
        CancellationToken cancellationToken = default)
    {
        var state = GetState(projectId);
        var owner = symbol as CodeAnalysis.Symbols.IScriptSymbol ?? symbol.ContainingScript;
        var results = ImmutableArray.CreateBuilder<DeclarationMatch>();
        if (owner is null || symbol is not (CodeAnalysis.Symbols.IScriptSymbol or CodeAnalysis.Symbols.IFunctionSymbol))
        {
            return results.ToImmutable();
        }

        foreach (var (name, path) in FindDerivedScripts(projectId, owner, transitive: true, cancellationToken))
        {
            var tree = state.Compilation.GetSyntaxTree(name);
            foreach (var declaration in CodeAnalysis.Structure.ScriptIndex.Get(tree).Declarations)
            {
                var matches = symbol is CodeAnalysis.Symbols.IScriptSymbol
                    ? declaration.Kind == CodeAnalysis.Structure.IndexedDeclarationKind.Script
                    : declaration.Kind is CodeAnalysis.Structure.IndexedDeclarationKind.Function
                        or CodeAnalysis.Structure.IndexedDeclarationKind.Event
                      && declaration.Name.Equals(symbol.Name.Text, StringComparison.OrdinalIgnoreCase);
                if (matches)
                {
                    results.Add(new DeclarationMatch(path, tree, declaration));
                }
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// The functions that call <paramref name="function"/>, each with the spans of its calls.
    /// </summary>
    public ImmutableArray<(CodeAnalysis.Symbols.IFunctionSymbol Caller, string Path, SyntaxTree Tree, ImmutableArray<TextSpan> Calls)> FindCallers(
        ProjectId projectId,
        CodeAnalysis.Symbols.IFunctionSymbol function,
        CancellationToken cancellationToken = default)
    {
        var state = GetState(projectId);
        var results = ImmutableArray.CreateBuilder<(CodeAnalysis.Symbols.IFunctionSymbol, string, SyntaxTree, ImmutableArray<TextSpan>)>();
        foreach (var script in FindReferences(projectId, function, cancellationToken))
        {
            var model = state.Compilation.GetSemanticModel(script.Tree);
            var byCaller = new Dictionary<CodeAnalysis.Symbols.IFunctionSymbol, ImmutableArray<TextSpan>.Builder>(ReferenceEqualityComparer.Instance);
            var order = new List<CodeAnalysis.Symbols.IFunctionSymbol>();
            foreach (var reference in script.Spans)
            {
                if (reference.IsDeclaration || model.GetEnclosingFunction(reference.Span.Start) is not { } caller)
                {
                    continue;
                }

                if (!byCaller.TryGetValue(caller, out var spans))
                {
                    spans = ImmutableArray.CreateBuilder<TextSpan>();
                    byCaller[caller] = spans;
                    order.Add(caller);
                }

                spans.Add(reference.Span);
            }

            foreach (var caller in order)
            {
                results.Add((caller, script.Path, script.Tree, byCaller[caller].ToImmutable()));
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Declarations across every project whose name matches <paramref name="query"/> as a
    /// case-insensitive subsequence (`mvto` finds `MoveTo`), from syntax alone. Each script
    /// file is searched once even when several projects include it.
    /// </summary>
    public ImmutableArray<DeclarationMatch> FindDeclarations(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Rank before limiting, so a loose subsequence match never crowds out an exact one.
        var ranked = new List<(int Rank, DeclarationMatch Match)>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _projects)
        {
            var state = pair.Value;
            foreach (var (name, path) in state.DiskResolver.GetScripts())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seenPaths.Add(path))
                {
                    continue;
                }

                var tree = state.Compilation.GetSyntaxTree(name);
                foreach (var declaration in CodeAnalysis.Structure.ScriptIndex.Get(tree).Declarations)
                {
                    var rank = MatchRank(query, declaration.Name);
                    if (rank >= 0)
                    {
                        ranked.Add((rank, new DeclarationMatch(path, tree, declaration)));
                    }
                }
            }
        }

        ranked.Sort(static (a, b) => a.Rank != b.Rank
            ? a.Rank.CompareTo(b.Rank)
            : string.Compare(a.Match.Declaration.Name, b.Match.Declaration.Name, StringComparison.OrdinalIgnoreCase));
        var results = ImmutableArray.CreateBuilder<DeclarationMatch>(Math.Min(limit, ranked.Count));
        for (var i = 0; i < ranked.Count && i < limit; i++)
        {
            results.Add(ranked[i].Match);
        }

        return results.MoveToImmutable();
    }

    // 0 exact, 1 prefix, 2 substring, 3 subsequence, -1 no match (all case-insensitive).
    private static int MatchRank(string query, string candidate)
    {
        if (candidate.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (candidate.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return IsSubsequence(query, candidate) ? 3 : -1;
    }

    private static bool IsSubsequence(string query, string candidate)
    {
        var q = 0;
        for (var c = 0; c < candidate.Length && q < query.Length; c++)
        {
            if (char.ToUpperInvariant(candidate[c]) == char.ToUpperInvariant(query[q]))
            {
                q++;
            }
        }

        return q == query.Length;
    }

    public ImmutableArray<DocumentId> GetDocumentIdsForPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ImmutableArray<DocumentId>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<DocumentId>();
        foreach (var pair in _projects)
        {
            if (pair.Value.DiskResolver.TryGetNameFromPath(path, out _))
            {
                builder.Add(DocumentId.Create(pair.Key, path));
            }
        }

        return builder.ToImmutable();
    }

    public bool TryGetSyntaxTree(DocumentId documentId, out SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        tree = null!;
        if (!_projects.TryGetValue(documentId.ProjectId, out var state)
            || !state.DiskResolver.TryGetNameFromPath(documentId.Path, out var name))
        {
            return false;
        }

        // GetSyntaxTree caches a missing-script entry for names it cannot
        // resolve, so refuse documents that neither have an open buffer nor
        // exist on disk rather than polluting the shared compilation cache.
        if (!_openDocuments.ContainsKey(documentId) && !File.Exists(documentId.Path))
        {
            return false;
        }

        tree = state.Compilation.GetSyntaxTree(name);
        return true;
    }

    /// <summary>
    /// Computes diagnostics for a single document, forcing that script's binding to
    /// complete so results are up to date even if nothing else has touched it yet.
    /// Returns an empty array for documents that cannot be resolved (e.g. neither
    /// open nor present on disk).
    /// </summary>
    public ImmutableArray<Diagnostic> GetDiagnostics(
        DocumentId documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        if (!_projects.TryGetValue(documentId.ProjectId, out var state)
            || !state.DiskResolver.TryGetNameFromPath(documentId.Path, out var name))
        {
            return ImmutableArray<Diagnostic>.Empty;
        }

        return state.Compilation.GetDiagnostics(name, cancellationToken);
    }

    public bool IsDocumentOpen(DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        return _openDocuments.ContainsKey(documentId);
    }

    public bool TryGetOpenDocumentText(DocumentId documentId, out SourceText text)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        return _openDocuments.TryGetValue(documentId, out text!);
    }

    public WorkspaceSnapshot Describe() => Describe(lastChangeKind: null);

    internal WorkspaceSnapshot Describe(string? lastChangeKind)
    {
        var dialects = ImmutableArray.CreateBuilder<string>(_projects.Count);
        var projectPaths = ImmutableArray.CreateBuilder<string>(_projects.Count);
        foreach (var pair in _projects)
        {
            var dialect = pair.Value.Project.Dialect;
            dialects.Add(dialect is null ? "Unknown" : dialect.Game.ToString());
            if (pair.Value.Project.Path is { } path)
            {
                projectPaths.Add(path);
            }
        }

        var documentPaths = ImmutableArray.CreateBuilder<string>(_openDocuments.Count);
        foreach (var pair in _openDocuments)
        {
            documentPaths.Add(pair.Key.Path);
        }

        return new WorkspaceSnapshot(
            _projects.Count,
            dialects.ToImmutable(),
            _openDocuments.Count,
            lastChangeKind,
            projectPaths.ToImmutable(),
            documentPaths.ToImmutable());
    }

    internal ImmutableDictionary<DocumentId, SourceText> OpenDocuments => _openDocuments;

    internal ProjectState GetState(ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        if (!_projects.TryGetValue(projectId, out var state))
        {
            throw new ArgumentException("Project is not in the solution.", nameof(projectId));
        }

        return state;
    }

    internal PapyrusSolution WithProject(ProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_projects.TryGetValue(state.Id, out var existing) && ReferenceEquals(existing, state))
        {
            return this;
        }

        return new PapyrusSolution(_projects.SetItem(state.Id, state), _openDocuments);
    }

    internal PapyrusSolution WithOpenDocument(DocumentId documentId, SourceText text)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(text);
        if (_openDocuments.TryGetValue(documentId, out var existing) && ReferenceEquals(existing, text))
        {
            return this;
        }

        return new PapyrusSolution(_projects, _openDocuments.SetItem(documentId, text));
    }

    internal PapyrusSolution WithoutOpenDocument(DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        if (!_openDocuments.ContainsKey(documentId))
        {
            return this;
        }

        return new PapyrusSolution(_projects, _openDocuments.Remove(documentId));
    }

    internal PapyrusSolution WithoutProject(ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        if (!_projects.ContainsKey(projectId))
        {
            return this;
        }

        var openDocuments = _openDocuments;
        foreach (var pair in _openDocuments)
        {
            if (pair.Key.ProjectId.Equals(projectId))
            {
                openDocuments = openDocuments.Remove(pair.Key);
            }
        }

        return new PapyrusSolution(_projects.Remove(projectId), openDocuments);
    }
}

/// <summary>
/// The set of reference locations found for a symbol within a single script.
/// </summary>
/// <summary>A declaration found by <see cref="PapyrusSolution.FindDeclarations"/>.</summary>
public sealed record DeclarationMatch(string Path, SyntaxTree Tree, CodeAnalysis.Structure.IndexedDeclaration Declaration);

public sealed class ScriptReferences
{
    public ScriptReferences(string path, SyntaxTree tree, ImmutableArray<ReferenceLocation> spans)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(tree);
        Path = path;
        Tree = tree;
        Spans = spans;
    }

    public string Path { get; }

    public SyntaxTree Tree { get; }

    public ImmutableArray<ReferenceLocation> Spans { get; }
}

internal sealed class ProjectState
{
    public ProjectState(PapyrusProject project, FileSystemScriptResolver diskResolver, PapyrusCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(diskResolver);
        ArgumentNullException.ThrowIfNull(compilation);
        if (project.Path is null)
        {
            throw new ArgumentException("Project has no path.", nameof(project));
        }

        Id = ProjectId.Create(project.Path);
        Project = project;
        DiskResolver = diskResolver;
        Compilation = compilation;
        _flags = new Lazy<CodeAnalysis.FlagsFile?>(() => LoadFlags(project, diskResolver));
    }

    private readonly Lazy<CodeAnalysis.FlagsFile?> _flags;

    /// <summary>
    /// The project's flags file, which gives user flags (Hidden, Conditional, ...) their
    /// bits when emitting. Found like the compiler finds it: relative to the project, else
    /// by file name in the import directories. Null when unset or unreadable.
    /// </summary>
    public CodeAnalysis.FlagsFile? Flags => _flags.Value;

    private static CodeAnalysis.FlagsFile? LoadFlags(PapyrusProject project, FileSystemScriptResolver diskResolver)
    {
        if (string.IsNullOrWhiteSpace(project.Flags) || Path.GetDirectoryName(project.Path) is not { } directory)
        {
            return null;
        }

        try
        {
            var path = Path.GetFullPath(Path.Combine(directory, project.Flags));
            if (!File.Exists(path))
            {
                var fileName = Path.GetFileName(project.Flags);
                path = "";
                foreach (var include in diskResolver.IncludeDirectories)
                {
                    var candidate = Path.Combine(include, fileName);
                    if (File.Exists(candidate))
                    {
                        path = candidate;
                        break;
                    }
                }
            }

            return path.Length == 0 ? null : CodeAnalysis.FlagsFile.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public ProjectId Id { get; }

    public PapyrusProject Project { get; }

    public FileSystemScriptResolver DiskResolver { get; }

    public PapyrusCompilation Compilation { get; }
}
