using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.Logging;
using Papyrus.CodeAnalysis;
using Papyrus.Pex;
using Pyro = LetsPlayWithFire.Pyro;

namespace Papyrus.Workspaces;

/// <summary>
/// A <c>.ppj</c> as Pyro resolves it: variables substituted, remote imports mapped to Pyro's
/// download cache, and scripts and the flags file found the way Pyro finds them.
/// </summary>
public sealed class PapyrusProject
{
    private PapyrusProject(Pyro.PapyrusProject resolved, ImmutableArray<Pyro.ProjectDiagnostic> diagnostics)
    {
        Resolved = resolved;
        Diagnostics = diagnostics;
        Path = System.IO.Path.GetFullPath(resolved.ProjectFile);
        Game = resolved.GameType switch
        {
            "tes5" => GameId.Skyrim,
            "sse" => GameId.SkyrimSpecialEdition,
            "fo4" => GameId.Fallout4,
            "sf1" => GameId.Starfield,
            _ => null,
        };

        var includes = ImmutableArray.CreateBuilder<SourceInclude>();
        var missing = ImmutableArray.CreateBuilder<string>();
        foreach (var import in resolved.Imports)
        {
            if (import.ResolvedPath.Length > 0)
            {
                includes.Add(new SourceInclude(import.Text.Length == 0 ? "." : import.Text, Full(import.ResolvedPath), IsImport: true));
            }
            else if (import.IsRemote)
            {
                missing.Add(import.Text);
            }
        }

        var folders = ImmutableArray.CreateBuilder<ProjectFolder>();
        foreach (var folder in resolved.Folders)
        {
            if (folder.ResolvedPath.Length > 0)
            {
                var directory = Full(folder.ResolvedPath);
                folders.Add(new ProjectFolder(directory, folder.NoRecurse));

                // A folder is usually also an import; search it once.
                if (!includes.Any(include => string.Equals(include.Directory, directory, StringComparison.OrdinalIgnoreCase)))
                {
                    includes.Add(new SourceInclude(folder.Text, directory, IsImport: false));
                }
            }
            else if (IsRemote(folder.Text))
            {
                missing.Add(folder.Text);
            }
        }

        Includes = includes.ToImmutable();
        Folders = folders.ToImmutable();
        MissingRemotes = missing.ToImmutable();
        Scripts = [.. resolved.Scripts.Values.Select(Full)];
        Output = resolved.Options.OutputPath.Length == 0 ? null : Full(resolved.Options.OutputPath);
        Flags = resolved.ResolvedFlagsPath.Length > 0 && System.IO.File.Exists(resolved.ResolvedFlagsPath) ? resolved.ResolvedFlagsPath : null;
    }

    /// <summary>
    /// Loads <paramref name="ppjPath"/> with Pyro. Problems in the file are reported in
    /// <see cref="Diagnostics"/> rather than thrown.
    /// </summary>
    /// <param name="pyro">
    /// The user's Pyro. Its download cache and access token serve remote imports. Without one,
    /// remote imports are left out and listed in <see cref="MissingRemotes"/>.
    /// </param>
    public static PapyrusProject Load(string ppjPath, PyroInstall? pyro = null, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppjPath);

        var result = Pyro.Projects.TryLoad(
            System.IO.Path.GetFullPath(ppjPath),
            new Pyro.BuildOptions { DryRun = pyro is null },
            logger is null ? null : new PyroLog(logger),
            new Pyro.ProjectHost { ProgramPath = pyro?.Directory ?? "", SkipInstalledGameLookup = true });
        return new PapyrusProject(result.Project, [.. result.Diagnostics]);
    }

    /// <summary>Pyro's view of the project, for what this type does not surface.</summary>
    public Pyro.PapyrusProject Resolved { get; }

    public string Path { get; }

    public ProjectId Id => ProjectId.Create(Path);

    public string Directory => System.IO.Path.GetDirectoryName(Path)!;

    public GameId? Game { get; }

    public PapyrusDialect? Dialect => Game switch
    {
        GameId.Skyrim => PapyrusDialect.SkyrimLe,
        GameId.SkyrimSpecialEdition => PapyrusDialect.SkyrimSe,
        GameId.Fallout4 => PapyrusDialect.Fallout4,
        GameId.Starfield => PapyrusDialect.Starfield,
        _ => null,
    };

    /// <summary>
    /// Where scripts are looked up, in order: Pyro's import paths (the project folder first),
    /// then the project's folders.
    /// </summary>
    public ImmutableArray<SourceInclude> Includes { get; }

    /// <summary>The project's own script folders, resolved.</summary>
    public ImmutableArray<ProjectFolder> Folders { get; }

    /// <summary>Full paths of the scripts Pyro would compile.</summary>
    public ImmutableArray<string> Scripts { get; }

    /// <summary>Remote imports and folders that are not in Pyro's cache, as written.</summary>
    public ImmutableArray<string> MissingRemotes { get; }

    public string? Output { get; }

    /// <summary>
    /// The flags file Pyro found, or null when there is none. Pyro names a default flags file when
    /// the project names none, so a missing file is not an error here.
    /// </summary>
    public string? Flags { get; }

    public ImmutableArray<Pyro.ProjectDiagnostic> Diagnostics { get; }

    /// <summary>The events Pyro runs during a build, in the order it runs them.</summary>
    public IEnumerable<Pyro.ProjectEvent> BuildEvents(params string[] names)
        => names.SelectMany(name => Resolved.Events.Where(e => e.UseInBuild && e.Name == name));

    private string Full(string path) => System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory, path));

    private static bool IsRemote(string text)
        => text.StartsWith("https:", StringComparison.OrdinalIgnoreCase) || text.StartsWith("http:", StringComparison.OrdinalIgnoreCase);

    private sealed class PyroLog(ILogger logger) : Pyro.IPyroLog
    {
        public void Write(Pyro.PyroLogLevel level, string message)
            => logger.Log(
                level switch
                {
                    Pyro.PyroLogLevel.Warning => LogLevel.Warning,
                    Pyro.PyroLogLevel.Error or Pyro.PyroLogLevel.Critical => LogLevel.Error,
                    _ => LogLevel.Debug,
                },
                "Pyro: {Message}",
                message);
    }
}

/// <summary>A directory scripts are looked up in, and the project text that named it.</summary>
public sealed record SourceInclude(string Declared, string Directory, bool IsImport);

public sealed record ProjectFolder(string Path, bool NoRecurse);
