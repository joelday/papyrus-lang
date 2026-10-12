using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Papyrus.Workspaces;

/// <summary>
/// Detects, from the project file alone, whether a project relies on
/// <see href="https://github.com/fireundubh/pyro">Pyro</see>: Pyro-only content the
/// workspace ignores, or resolution only Pyro performs (remote imports, <c>@Variable</c>
/// substitution). See <c>docs/spec/ppj-pyro-parity.md</c>.
/// </summary>
public static class PyroRequirements
{
    public const string ReleasesUrl = "https://github.com/fireundubh/pyro/releases/latest";

    /// <summary>Human-readable reasons the project needs Pyro; empty when it does not.</summary>
    public static ImmutableArray<string> Analyze(PapyrusProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var reasons = ImmutableArray.CreateBuilder<string>();

        if (!project.Ignored.IsEmpty)
        {
            reasons.Add("uses Pyro-only content: " + string.Join(", ", project.Ignored.Select(IgnoredProjectContent.FormatName)));
        }

        var remotes = project.Imports.Where(IsRemote).ToImmutableArray();
        if (!remotes.IsEmpty)
        {
            reasons.Add("imports from a remote that only Pyro fetches: " + string.Join(", ", remotes));
        }

        var variables = AllPathValues(project).SelectMany(FindVariableTokens).Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray();
        if (!variables.IsEmpty)
        {
            reasons.Add("uses @Variables that only Pyro expands: " + string.Join(", ", variables));
        }

        return reasons.ToImmutable();
    }

    private static bool IsRemote(string import)
        => import.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || import.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> AllPathValues(PapyrusProject project)
    {
        if (project.Output is not null)
            yield return project.Output;
        if (project.Flags is not null)
            yield return project.Flags;
        foreach (var import in project.Imports)
            yield return import;
        foreach (var folder in project.Folders)
            yield return folder.Path;
        foreach (var script in project.Scripts)
            yield return script;
        foreach (var command in project.PreBuild)
            yield return command;
        foreach (var command in project.PostBuild)
            yield return command;
    }

    // Pyro variables are @Name tokens; an email-like "a@b" is not one because the name must start the token.
    private static IEnumerable<string> FindVariableTokens(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '@' || (i > 0 && IsNameChar(value[i - 1])))
                continue;

            var end = i + 1;
            while (end < value.Length && IsNameChar(value[end]))
                end++;

            if (end > i + 1)
                yield return value[i..end];
        }
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
