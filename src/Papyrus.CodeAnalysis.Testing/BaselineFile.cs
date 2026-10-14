using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Papyrus.CodeAnalysis.Testing;

public sealed class BaselineComparison
{
    public BaselineComparison(bool isMatch, ImmutableArray<string> errors)
    {
        IsMatch = isMatch;
        Errors = errors;
    }

    public bool IsMatch { get; }
    public ImmutableArray<string> Errors { get; }
}

/// <summary>
/// Load, save, and compare committed hash baselines. Writing baselines is
/// maintainer-only and must carry an <see cref="AttestationNote"/>.
/// </summary>
public static class BaselineFile
{
    public static BaselineDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string json = File.ReadAllText(path);
        return Parse(json);
    }

    public static BaselineDocument Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("baseline JSON is empty.");

        BaselineDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, BaselineJsonContext.Default.BaselineDocument);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"invalid baseline JSON: {ex.Message}", ex);
        }

        if (document is null)
            throw new InvalidOperationException("baseline JSON is empty.");

        document.Scripts = WithOrdinalIgnoreCase(document.Scripts);
        return document;
    }

    public static BaselineDocument FromHashes(
        string game,
        IEnumerable<PexHashEntry> hashes,
        AttestationNote? attestation = null,
        string? pexVersion = null,
        string? generated = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(game);
        ArgumentNullException.ThrowIfNull(hashes);

        var document = new BaselineDocument
        {
            Game = game,
            PexVersion = pexVersion,
            Generated = generated ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Attestation = attestation is null ? null : BaselineAttestation.From(attestation),
            Scripts = new Dictionary<string, BaselineScript>(StringComparer.OrdinalIgnoreCase),
        };

        foreach (PexHashEntry entry in hashes)
        {
            document.Scripts[entry.ScriptName] = new BaselineScript
            {
                Sha256 = entry.Sha256,
                Status = "identical",
            };
        }

        return document;
    }

    public static void Save(string path, BaselineDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, Format(document));
    }

    public static string Format(BaselineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var ordered = new BaselineDocument
        {
            Game = document.Game,
            PexVersion = document.PexVersion,
            Generated = document.Generated,
            Attestation = document.Attestation,
            Scripts = new Dictionary<string, BaselineScript>(StringComparer.Ordinal),
        };

        var names = new List<string>(document.Scripts.Keys);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string name in names)
            ordered.Scripts[name] = document.Scripts[name];

        string json = JsonSerializer.Serialize(ordered, BaselineJsonContext.Default.BaselineDocument);
        if (!json.EndsWith('\n'))
            json += "\n";
        return json;
    }

    /// <summary>
    /// Writes a baseline only when <paramref name="attestation"/> parses.
    /// </summary>
    public static BaselineDocument WriteBaselines(
        string path,
        string game,
        IEnumerable<PexHashEntry> hashes,
        AttestationNote attestation,
        string? pexVersion = null,
        string? generated = null)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        BaselineDocument document = FromHashes(game, hashes, attestation, pexVersion, generated);
        Save(path, document);
        return document;
    }

    public static BaselineComparison Compare(BaselineDocument expected, IEnumerable<PexHashEntry> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var actualByName = new Dictionary<string, PexHashEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (PexHashEntry entry in actual)
            actualByName[entry.ScriptName] = entry;

        var errors = ImmutableArray.CreateBuilder<string>();
        var expectedNames = new List<string>(expected.Scripts.Keys);
        expectedNames.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string name in expectedNames)
        {
            BaselineScript script = expected.Scripts[name];
            if (!actualByName.TryGetValue(name, out PexHashEntry? entry))
            {
                errors.Add($"missing from source: {name}");
                continue;
            }

            if (!string.Equals(script.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"hash mismatch: {name}{Environment.NewLine}  expected: {script.Sha256}{Environment.NewLine}  actual:   {entry.Sha256}");
            }
        }

        var extra = new List<string>();
        foreach (string name in actualByName.Keys)
        {
            if (!expected.Scripts.ContainsKey(name))
                extra.Add(name);
        }

        extra.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string name in extra)
            errors.Add($"missing from baseline: {name}");

        ImmutableArray<string> list = errors.ToImmutable();
        return new BaselineComparison(list.Length == 0, list);
    }

    public static BaselineComparison CompareFile(string baselinePath, IEnumerable<PexHashEntry> actual)
    {
        return Compare(Load(baselinePath), actual);
    }

    /// <summary>
    /// Line-oriented fixture format used by public CI (not the vanilla game scripts):
    /// <c>script-name  sha256</c>. Lines starting with <c>#</c> are ignored.
    /// </summary>
    public static Dictionary<string, string> ParseHashList(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            string[] parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                throw new InvalidOperationException($"invalid hash-list line: {rawLine}");

            result[parts[0]] = parts[1].Trim().ToLowerInvariant();
        }

        return result;
    }

    public static string FormatHashList(IEnumerable<PexHashEntry> hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);
        var entries = new List<PexHashEntry>(hashes);
        entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.ScriptName, b.ScriptName));

        var sb = new StringBuilder();
        sb.AppendLine("# name  sha256  (normalized PEX; synthetic fixtures only)");
        foreach (PexHashEntry entry in entries)
            sb.Append(entry.ScriptName).Append("  ").AppendLine(entry.Sha256);
        return sb.ToString();
    }

    private static Dictionary<string, BaselineScript> WithOrdinalIgnoreCase(Dictionary<string, BaselineScript> scripts)
    {
        if (scripts.Comparer == StringComparer.OrdinalIgnoreCase)
            return scripts;

        var copy = new Dictionary<string, BaselineScript>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, BaselineScript> pair in scripts)
            copy[pair.Key] = pair.Value;
        return copy;
    }
}
