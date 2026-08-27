using System;
using System.Collections.Immutable;
using System.Text;
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
        ImmutableArray<string> preBuild = default,
        ImmutableArray<string> postBuild = default,
        string? path = null)
    {
        Game = game;
        Imports = imports.IsDefault ? ImmutableArray<string>.Empty : imports;
        Folders = folders.IsDefault ? ImmutableArray<ProjectFolder>.Empty : folders;
        Scripts = scripts.IsDefault ? ImmutableArray<string>.Empty : scripts;
        Variables = variables;
        Output = output;
        Flags = flags;
        Optimize = optimize;
        Release = release;
        Final = final;
        Asm = asm;
        PreBuild = preBuild.IsDefault ? ImmutableArray<string>.Empty : preBuild;
        PostBuild = postBuild.IsDefault ? ImmutableArray<string>.Empty : postBuild;
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

    public ImmutableArray<string> PreBuild { get; }

    public ImmutableArray<string> PostBuild { get; }

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
            PreBuild,
            PostBuild,
            full);
    }

    public bool TryExpandEnvironmentVariables(out PapyrusProject expanded, out string missingName)
    {
        if (!TryExpandValue(Output, out var output, out missingName)
            || !TryExpandValue(Flags, out var flags, out missingName)
            || !TryExpandStrings(Imports, out var imports, out missingName)
            || !TryExpandFolders(Folders, out var folders, out missingName)
            || !TryExpandStrings(Scripts, out var scripts, out missingName)
            || !TryExpandVariables(Variables, out var variables, out missingName)
            || !TryExpandStrings(PreBuild, out var preBuild, out missingName)
            || !TryExpandStrings(PostBuild, out var postBuild, out missingName))
        {
            expanded = this;
            return false;
        }

        if (ReferenceEquals(output, Output)
            && ReferenceEquals(flags, Flags)
            && imports.Equals(Imports)
            && folders.Equals(Folders)
            && scripts.Equals(Scripts)
            && variables.Equals(Variables)
            && preBuild.Equals(PreBuild)
            && postBuild.Equals(PostBuild))
        {
            expanded = this;
            return true;
        }

        expanded = new PapyrusProject(
            Game,
            imports,
            folders,
            scripts,
            variables,
            output,
            flags,
            Optimize,
            Release,
            Final,
            Asm,
            preBuild,
            postBuild,
            Path);
        return true;
    }

    private static bool TryExpandValue(string? value, out string? expanded, out string missingName)
    {
        missingName = "";
        if (value is null)
        {
            expanded = null;
            return true;
        }

        if (!TryReplaceEnvTokens(value, out expanded, out missingName))
        {
            return false;
        }

        return true;
    }

    private static bool TryExpandStrings(
        ImmutableArray<string> values,
        out ImmutableArray<string> expanded,
        out string missingName)
    {
        missingName = "";
        if (values.IsDefaultOrEmpty)
        {
            expanded = values.IsDefault ? ImmutableArray<string>.Empty : values;
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<string>(values.Length);
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            if (!TryReplaceEnvTokens(values[i], out var next, out missingName))
            {
                expanded = values;
                return false;
            }

            changed |= !ReferenceEquals(next, values[i]);
            builder.Add(next);
        }

        expanded = changed ? builder.MoveToImmutable() : values;
        return true;
    }

    private static bool TryExpandFolders(
        ImmutableArray<ProjectFolder> folders,
        out ImmutableArray<ProjectFolder> expanded,
        out string missingName)
    {
        missingName = "";
        if (folders.IsDefaultOrEmpty)
        {
            expanded = folders.IsDefault ? ImmutableArray<ProjectFolder>.Empty : folders;
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<ProjectFolder>(folders.Length);
        var changed = false;
        for (var i = 0; i < folders.Length; i++)
        {
            var folder = folders[i];
            if (!TryReplaceEnvTokens(folder.Path, out var path, out missingName))
            {
                expanded = folders;
                return false;
            }

            if (ReferenceEquals(path, folder.Path))
            {
                builder.Add(folder);
                continue;
            }

            changed = true;
            builder.Add(new ProjectFolder(path, folder.NoRecurse));
        }

        expanded = changed ? builder.MoveToImmutable() : folders;
        return true;
    }

    private static bool TryExpandVariables(
        ImmutableDictionary<string, string> variables,
        out ImmutableDictionary<string, string> expanded,
        out string missingName)
    {
        missingName = "";
        expanded = variables;
        if (variables.IsEmpty)
        {
            return true;
        }

        ImmutableDictionary<string, string>.Builder? builder = null;
        foreach (var pair in variables)
        {
            if (!TryReplaceEnvTokens(pair.Value, out var value, out missingName))
            {
                return false;
            }

            if (ReferenceEquals(value, pair.Value))
            {
                continue;
            }

            builder ??= variables.ToBuilder();
            builder[pair.Key] = value;
        }

        if (builder is not null)
        {
            expanded = builder.ToImmutable();
        }

        return true;
    }

    // Unset variables are errors. Empty-but-set values expand to empty, matching process environment.
    private static bool TryReplaceEnvTokens(string value, out string expanded, out string missingName)
    {
        missingName = "";
        var start = value.IndexOf("${", StringComparison.Ordinal);
        if (start < 0)
        {
            expanded = value;
            return true;
        }

        var builder = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length)
        {
            if (i + 1 < value.Length && value[i] == '$' && value[i + 1] == '{')
            {
                var close = value.IndexOf('}', i + 2);
                if (close < 0)
                {
                    builder.Append(value, i, value.Length - i);
                    break;
                }

                var nameLength = close - (i + 2);
                if (nameLength > 0 && IsEnvironmentName(value, i + 2, nameLength))
                {
                    var name = value.Substring(i + 2, nameLength);
                    var env = Environment.GetEnvironmentVariable(name);
                    if (env is null)
                    {
                        expanded = value;
                        missingName = name;
                        return false;
                    }

                    builder.Append(env);
                    i = close + 1;
                    continue;
                }

                builder.Append(value, i, close - i + 1);
                i = close + 1;
                continue;
            }

            builder.Append(value[i]);
            i++;
        }

        expanded = builder.ToString();
        return true;
    }

    private static bool IsEnvironmentName(string value, int start, int length)
    {
        var first = value[start];
        if (!IsEnvironmentNameStart(first))
        {
            return false;
        }

        for (var i = 1; i < length; i++)
        {
            var c = value[start + i];
            if (!IsEnvironmentNameStart(c) && (c < '0' || c > '9'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsEnvironmentNameStart(char c)
    {
        return c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';
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
