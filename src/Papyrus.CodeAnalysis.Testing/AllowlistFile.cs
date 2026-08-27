using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Papyrus.CodeAnalysis.Testing;

public sealed class AllowlistEntry
{
    public AllowlistEntry(
        int number,
        string id,
        string justification,
        ImmutableArray<string> scripts = default)
    {
        Number = number;
        Id = id;
        Justification = justification;
        Scripts = scripts.IsDefault ? ImmutableArray<string>.Empty : scripts;
    }

    public int Number { get; }

    public string Id { get; }

    public string Justification { get; }

    /// <summary>
    /// Explicit script names waived by this construct. Not parsed from prose.
    /// </summary>
    public ImmutableArray<string> Scripts { get; }
}

public sealed class AllowlistDocument
{
    public AllowlistDocument(ImmutableArray<AllowlistEntry> entries)
    {
        Entries = entries.IsDefault ? ImmutableArray<AllowlistEntry>.Empty : entries;
    }

    public ImmutableArray<AllowlistEntry> Entries { get; }

    public int Size => Entries.Length;

    public bool ContainsScript(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
            return false;

        foreach (var entry in Entries)
        {
            foreach (var listed in entry.Scripts)
            {
                if (ScriptNamesEqual(listed, scriptName))
                    return true;
            }
        }

        return false;
    }

    public bool ContainsId(string allowlistId)
    {
        if (string.IsNullOrWhiteSpace(allowlistId))
            return false;

        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Id, allowlistId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    entry.Number.ToString(CultureInfo.InvariantCulture),
                    allowlistId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ScriptNamesEqual(string listed, string actual)
    {
        return string.Equals(BaseScriptName(listed), BaseScriptName(actual), StringComparison.OrdinalIgnoreCase);
    }

    internal static string BaseScriptName(string name)
    {
        var text = name.Replace('\\', '/').Trim();
        var slash = text.LastIndexOf('/');
        if (slash >= 0)
            text = text[(slash + 1)..];
        var colon = text.LastIndexOf(':');
        if (colon >= 0)
            text = text[(colon + 1)..];
        var dot = text.IndexOf('.');
        if (dot >= 0)
            text = text[..dot];
        return text;
    }
}

/// <summary>
/// Reviewed-as-code list of semantic-equivalence-only constructs.
/// Numbered markdown items; CI reports <see cref="AllowlistDocument.Size"/>.
/// </summary>
public static class AllowlistFile
{
    private static readonly Regex s_item = new(
        @"^\s*(\d+)\.\s+(?:`([^`]+)`\s*(?:—|--|:)\s*)?(.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static AllowlistDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var entries = ImmutableArray.CreateBuilder<AllowlistEntry>();
        AllowlistEntry? pending = null;
        var justification = new List<string>();
        var scripts = new List<string>();

        void Flush()
        {
            if (pending is null)
                return;

            var body = string.Join(" ", justification).Trim();
            entries.Add(new AllowlistEntry(pending.Number, pending.Id, body, scripts.ToImmutableArray()));
            pending = null;
            justification.Clear();
            scripts.Clear();
        }

        foreach (string raw in text.Split(['\r', '\n']))
        {
            var match = s_item.Match(raw);
            if (match.Success)
            {
                Flush();
                var number = int.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
                var id = match.Groups[2].Success && match.Groups[2].Value.Length > 0
                    ? match.Groups[2].Value.Trim()
                    : number.ToString(CultureInfo.InvariantCulture);
                pending = new AllowlistEntry(number, id, "");
                var rest = match.Groups[3].Value.Trim();
                if (!TryTakeScripts(rest, scripts))
                    justification.Add(rest);
                continue;
            }

            if (pending is null)
                continue;

            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                Flush();
                continue;
            }

            if (TryTakeScripts(line, scripts))
                continue;

            justification.Add(line);
        }

        Flush();
        return new AllowlistDocument(entries.ToImmutable());
    }

    public static AllowlistDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    public static AllowlistDocument LoadOrEmpty(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new AllowlistDocument(ImmutableArray<AllowlistEntry>.Empty);

        return Load(path);
    }

    /// <summary>
    /// Walks from <paramref name="startDirectory"/> (or the current directory)
    /// looking for <c>docs/compliance/allowlist.md</c> at the repo root.
    /// </summary>
    public static string? FindDefaultPath(string? startDirectory = null)
    {
        DirectoryInfo? dir;
        try
        {
            dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "docs", "compliance", "allowlist.md");
            if (File.Exists(candidate))
                return candidate;

            if (File.Exists(Path.Combine(dir.FullName, "Papyrus.sln")))
                return null;

            dir = dir.Parent;
        }

        return null;
    }

    private static bool TryTakeScripts(string line, List<string> scripts)
    {
        var text = line.Trim();
        if (text.StartsWith("- ", StringComparison.Ordinal))
            text = text[2..].Trim();

        const string prefix = "scripts:";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (string rawPart in text[prefix.Length..].Split(','))
        {
            var part = rawPart.Trim().Trim('`');
            if (part.Length == 0)
                continue;

            var name = AllowlistDocument.BaseScriptName(part);
            if (name.Length == 0)
                continue;

            var exists = false;
            foreach (var listed in scripts)
            {
                if (AllowlistDocument.ScriptNamesEqual(listed, name))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                scripts.Add(name);
        }

        return true;
    }
}
