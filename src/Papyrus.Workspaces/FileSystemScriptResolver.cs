using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.Workspaces;

public sealed class FileSystemScriptResolver : ScriptResolver
{
    /// <param name="game">
    /// Null allows namespaced lookup via <see cref="QualifiedName.ToRelativePath"/>.
    /// Skyrim and SSE reject multi-part names.
    /// </param>
    public FileSystemScriptResolver(
        IEnumerable<string> includeDirectories,
        string? outputDirectory = null,
        GameId? game = null)
    {
        ArgumentNullException.ThrowIfNull(includeDirectories);

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var directory in includeDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            builder.Add(Path.GetFullPath(directory));
        }

        IncludeDirectories = builder.ToImmutable();
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? null
            : Path.GetFullPath(outputDirectory);
        Game = game;
    }

    public FileSystemScriptResolver(PapyrusProject project, string projectDirectory)
        : this(
            IncludePathsFromProject(project, projectDirectory),
            OutputPathFromProject(project, projectDirectory),
            project.Game)
    {
    }

    public ImmutableArray<string> IncludeDirectories { get; }

    public string? OutputDirectory { get; }

    public GameId? Game { get; }

    public override bool TryResolve(QualifiedName name, out SourceText text, out string displayPath)
    {
        text = null!;
        displayPath = string.Empty;
        if (!TryGetRelativeSourcePath(name, out var relativePath))
        {
            return false;
        }

        for (var i = 0; i < IncludeDirectories.Length; i++)
        {
            var path = Path.Combine(IncludeDirectories[i], relativePath);
            if (TryReadSource(path, out text))
            {
                displayPath = path;
                return true;
            }
        }

        return false;
    }

    public override bool TryResolvePex(string colonName, out PexScriptMetadata metadata)
    {
        metadata = null!;
        if (string.IsNullOrEmpty(colonName))
        {
            return false;
        }

        var name = QualifiedName.Parse(colonName);
        if (!TryGetRelativeSourcePath(name, out var relativeSource))
        {
            return false;
        }

        var relativePex = Path.ChangeExtension(relativeSource, ".pex");
        for (var i = 0; i < IncludeDirectories.Length; i++)
        {
            if (TryReadMatchingPex(Path.Combine(IncludeDirectories[i], relativePex), colonName, out metadata))
            {
                return true;
            }
        }

        if (OutputDirectory is not null)
        {
            if (TryReadMatchingPex(Path.Combine(OutputDirectory, relativePex), colonName, out metadata))
            {
                return true;
            }

            var parts = name.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : name.Parts;
            if (parts.Length > 1
                && TryReadMatchingPex(Path.Combine(OutputDirectory, parts[^1].Text + ".pex"), colonName, out metadata))
            {
                return true;
            }
        }

        return false;
    }

    internal ImmutableArray<string> CandidateSourcePaths(QualifiedName name)
    {
        if (!TryGetRelativeSourcePath(name, out var relativePath))
        {
            return ImmutableArray<string>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<string>(IncludeDirectories.Length);
        for (var i = 0; i < IncludeDirectories.Length; i++)
        {
            builder.Add(Path.Combine(IncludeDirectories[i], relativePath));
        }

        return builder.MoveToImmutable();
    }

    internal bool TryGetNameFromPath(string path, out QualifiedName name)
    {
        name = default;
        if (string.IsNullOrWhiteSpace(path) || !HasPscExtension(path))
        {
            return false;
        }

        var full = Path.GetFullPath(path);
        string? bestInclude = null;
        for (var i = 0; i < IncludeDirectories.Length; i++)
        {
            var include = IncludeDirectories[i];
            if (!IsUnderDirectory(include, full))
            {
                continue;
            }

            if (bestInclude is null || include.Length > bestInclude.Length)
            {
                bestInclude = include;
            }
        }

        if (bestInclude is null || !TryGetRelativeName(bestInclude, full, out name))
        {
            name = default;
            return false;
        }

        if (!SupportsNamespaces && name.Parts.Length > 1)
        {
            name = default;
            return false;
        }

        return name.Parts.Length > 0;
    }

    private static bool HasPscExtension(string path)
    {
        return Path.GetExtension(path).Equals(".psc", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderDirectory(string directory, string fullPath)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.Length <= prefix.Length)
        {
            return false;
        }

        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separator = fullPath[prefix.Length];
        return separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar;
    }

    private static bool TryGetRelativeName(string include, string fullPath, out QualifiedName name)
    {
        name = default;
        var prefix = include.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.Length <= prefix.Length)
        {
            return false;
        }

        var relative = fullPath[(prefix.Length + 1)..].Replace('\\', '/');
        if (relative.EndsWith(".psc", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[..^4];
        }

        if (relative.Length == 0 || relative.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        name = QualifiedName.Parse(relative.Replace('/', ':'));
        return name.Parts.Length > 0;
    }

    private bool TryGetRelativeSourcePath(QualifiedName name, out string relativePath)
    {
        var parts = name.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : name.Parts;
        if (parts.IsEmpty)
        {
            relativePath = string.Empty;
            return false;
        }

        if (!SupportsNamespaces && parts.Length > 1)
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = name.ToRelativePath().Replace('/', Path.DirectorySeparatorChar);
        return true;
    }

    private bool SupportsNamespaces => Game is not (GameId.Skyrim or GameId.SkyrimSpecialEdition);

    private bool TryReadSource(string path, out SourceText text)
    {
        text = null!;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            text = SourceText.From(File.ReadAllBytes(path));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool TryReadMatchingPex(string path, string colonName, out PexScriptMetadata metadata)
    {
        if (!TryReadPex(path, out metadata))
        {
            return false;
        }

        if (metadata.Name.Text.Equals(colonName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        metadata = null!;
        return false;
    }

    private bool TryReadPex(string path, out PexScriptMetadata metadata)
    {
        metadata = null!;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var file = PexReader.Read(File.ReadAllBytes(path), Game);
            metadata = PexReflector.Reflect(file);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PexFormatException)
        {
            return false;
        }
    }

    private static IEnumerable<string> IncludePathsFromProject(PapyrusProject project, string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projectDirectory);

        foreach (var import in project.Imports)
        {
            yield return ResolveProjectPath(projectDirectory, import);
        }

        foreach (var folder in project.Folders)
        {
            yield return ResolveProjectPath(projectDirectory, folder.Path);
        }
    }

    private static string? OutputPathFromProject(PapyrusProject project, string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projectDirectory);
        return project.Output is null ? null : ResolveProjectPath(projectDirectory, project.Output);
    }

    private static string ResolveProjectPath(string projectDirectory, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(projectDirectory, path));
    }
}
