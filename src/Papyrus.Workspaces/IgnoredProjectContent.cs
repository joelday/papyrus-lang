using System;
using System.Collections.Immutable;
using System.Linq;

namespace Papyrus.Workspaces;

/// <summary>Wording shared by every loader for <see cref="PapyrusProject.Ignored"/>.</summary>
public static class IgnoredProjectContent
{
    public const string ParityDocument = "docs/spec/ppj-pyro-parity.md";

    public static string Describe(string projectPath, ImmutableArray<string> ignored)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        var names = string.Join(", ", ignored.Select(name =>
            name.StartsWith('@') ? name[1..] + " attribute" : "<" + name + ">"));
        return $"Papyrus project '{projectPath}' contains Pyro-only content the workspace does not use: {names}. "
            + $"Keep using Pyro for that; the editor only reads what decides which scripts and imports are in the project. See {ParityDocument}.";
    }
}
