using System;
using System.Collections.Generic;
using System.Text;

namespace Papyrus.CodeAnalysis.Testing;

/// <summary>
/// Maintainer attestation required by <c>--write-baselines</c>.
/// Hashes produced from a leaked compiler are a supply-chain incident.
/// </summary>
public sealed class AttestationNote
{
    public AttestationNote(
        string os,
        string papyruscVersion,
        string ckCompilerPathSha256,
        string ckFileVersion,
        string statement)
    {
        Os = os;
        PapyruscVersion = papyruscVersion;
        CkCompilerPathSha256 = ckCompilerPathSha256;
        CkFileVersion = ckFileVersion;
        Statement = statement;
    }

    public string Os { get; }
    public string PapyruscVersion { get; }
    public string CkCompilerPathSha256 { get; }
    public string CkFileVersion { get; }
    public string Statement { get; }

    public static bool TryParse(string text, out AttestationNote note, out string error)
    {
        note = null!;
        error = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "attestation note is empty";
            return false;
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (line.StartsWith("- ", StringComparison.Ordinal))
                line = line[2..].Trim();

            int colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            string key = NormalizeKey(line[..colon]);
            string value = line[(colon + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
                continue;

            fields[key] = value;
        }

        if (!TryGet(fields, out string os, "machine os", "os"))
        {
            error = "attestation note is missing 'Machine OS'";
            return false;
        }

        if (!TryGet(fields, out string version, "papyrusc version", "papyrusc"))
        {
            error = "attestation note is missing 'papyrusc version'";
            return false;
        }

        if (!TryGet(fields, out string ckHash, "ck compiler path sha256", "ck compiler path hash", "ck path sha256"))
        {
            error = "attestation note is missing 'CK compiler path SHA-256'";
            return false;
        }

        if (!TryGet(fields, out string ckVersion, "ck file version"))
        {
            error = "attestation note is missing 'CK file version'";
            return false;
        }

        if (!TryGet(fields, out string statement, "statement"))
        {
            error = "attestation note is missing 'Statement'";
            return false;
        }

        if (statement.IndexOf("legitimate", StringComparison.OrdinalIgnoreCase) < 0)
        {
            error = "attestation statement must declare a legitimate game/CK install";
            return false;
        }

        note = new AttestationNote(os, version, ckHash, ckVersion, statement);
        return true;
    }

    public string ToNoteText()
    {
        var sb = new StringBuilder();
        sb.Append("Machine OS: ").AppendLine(Os);
        sb.Append("papyrusc version: ").AppendLine(PapyruscVersion);
        sb.Append("CK compiler path SHA-256: ").AppendLine(CkCompilerPathSha256);
        sb.Append("CK file version: ").AppendLine(CkFileVersion);
        sb.Append("Statement: ").AppendLine(Statement);
        return sb.ToString();
    }

    private static string NormalizeKey(string key)
    {
        var sb = new StringBuilder(key.Length);
        foreach (char c in key.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c == ' ')
                sb.Append(c);
        }

        return CollapseSpaces(sb.ToString());
    }

    private static string CollapseSpaces(string value)
    {
        var sb = new StringBuilder(value.Length);
        var pending = false;
        foreach (char c in value)
        {
            if (c == ' ')
            {
                pending = sb.Length > 0;
                continue;
            }

            if (pending)
            {
                sb.Append(' ');
                pending = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool TryGet(Dictionary<string, string> fields, out string value, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (fields.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value))
                return true;
        }

        value = null!;
        return false;
    }
}
