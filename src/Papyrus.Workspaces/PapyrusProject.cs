using System;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis;
using Papyrus.Pex;

namespace Papyrus.Workspaces;

public sealed class PapyrusProject
{
    public PapyrusProject(
        GameId? game,
        ImmutableArray<string> imports,
        ImmutableArray<ProjectFolder> folders,
        ImmutableArray<string> scripts,
        ImmutableDictionary<string, string> variables,
        string? output,
        string? flags,
        bool optimize,
        bool release,
        bool final,
        AsmKind? asm = null,
        string? path = null)
    {
        Game = game;
        Imports = imports;
        Folders = folders;
        Scripts = scripts;
        Variables = variables;
        Output = output;
        Flags = flags;
        Optimize = optimize;
        Release = release;
        Final = final;
        Asm = asm;
        Path = path;
    }

    public GameId? Game { get; }

    public PapyrusDialect? Dialect => Game switch
    {
        GameId.Skyrim => PapyrusDialect.SkyrimLe,
        GameId.SkyrimSpecialEdition => PapyrusDialect.SkyrimSe,
        GameId.Fallout4 => PapyrusDialect.Fallout4,
        GameId.Fallout76 => PapyrusDialect.Fallout76,
        GameId.Starfield => PapyrusDialect.Starfield,
        _ => null,
    };

    public ImmutableArray<string> Imports { get; }

    public ImmutableArray<ProjectFolder> Folders { get; }

    public ImmutableArray<string> Scripts { get; }

    public ImmutableDictionary<string, string> Variables { get; }

    public string? Output { get; }

    public string? Flags { get; }

    public bool Optimize { get; }

    public bool Release { get; }

    public bool Final { get; }

    public AsmKind? Asm { get; }

    public string? Path { get; }

    public ProjectId Id => ProjectId.Create(Path ?? throw new InvalidOperationException("Project has no path."));

    public PapyrusProject WithPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = System.IO.Path.GetFullPath(path);
        if (Path is not null && string.Equals(Path, full, StringComparison.OrdinalIgnoreCase))
        {
            return this;
        }

        return new PapyrusProject(
            Game,
            Imports,
            Folders,
            Scripts,
            Variables,
            Output,
            Flags,
            Optimize,
            Release,
            Final,
            Asm,
            full);
    }
}

public sealed class ProjectFolder
{
    public ProjectFolder(string path, bool noRecurse = false)
    {
        Path = path;
        NoRecurse = noRecurse;
    }

    public string Path { get; }

    public bool NoRecurse { get; }
}

public enum AsmKind
{
    None,
    Keep,
    Only,
    Discard,
}
