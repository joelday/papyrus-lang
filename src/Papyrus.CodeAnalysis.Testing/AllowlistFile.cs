using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Papyrus.CodeAnalysis.Testing;

public sealed class AllowlistEntry
{
    public AllowlistEntry(int number, string id, string justification)
    {
        Number = number;
        Id = id;
        Justification = justification;
    }

    public int Number { get; }

    public string Id { get; }

    public string Justification { get; }
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
            if (ContainsName(entry.Justification, scriptName) || ContainsName(entry.Id, scriptName))
                return true;
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
                || string.Equals(entry.Number.ToString(), allowlistId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsName(string text, string scriptName)
    {
        var index = text.IndexOf(scriptName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        var before = index == 0 || !IsNameChar(text[index - 1]);
        var afterIndex = index + scriptName.Length;
        var after = afterIndex >= text.Length || !IsNameChar(text[afterIndex]);
        return before && after;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';
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

        void Flush()
        {
            if (pending is null)
                return;

            var body = string.Join(" ", justification).Trim();
            entries.Add(new AllowlistEntry(pending.Number, pending.Id, body));
            pending = null;
            justification.Clear();
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
                justification.Add(match.Groups[3].Value.Trim());
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
}
