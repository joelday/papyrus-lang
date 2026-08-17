using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Papyrus.CodeAnalysis.Testing;

/// <summary>
/// Committed tier-1 baseline. Hashes only — no corpus source, no Bethesda output.
/// </summary>
public sealed class BaselineDocument
{
    [JsonPropertyName("game")]
    public string Game { get; set; } = "";

    [JsonPropertyName("pexVersion")]
    public string? PexVersion { get; set; }

    [JsonPropertyName("generated")]
    public string Generated { get; set; } = "";

    [JsonPropertyName("attestation")]
    public BaselineAttestation? Attestation { get; set; }

    [JsonPropertyName("scripts")]
    public Dictionary<string, BaselineScript> Scripts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BaselineScript
{
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "identical";

    [JsonPropertyName("allowlistId")]
    public string? AllowlistId { get; set; }
}

public sealed class BaselineAttestation
{
    [JsonPropertyName("os")]
    public string Os { get; set; } = "";

    [JsonPropertyName("papyruscVersion")]
    public string PapyruscVersion { get; set; } = "";

    [JsonPropertyName("ckCompilerPathSha256")]
    public string CkCompilerPathSha256 { get; set; } = "";

    [JsonPropertyName("ckFileVersion")]
    public string CkFileVersion { get; set; } = "";

    [JsonPropertyName("statement")]
    public string Statement { get; set; } = "";

    public static BaselineAttestation From(AttestationNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new BaselineAttestation
        {
            Os = note.Os,
            PapyruscVersion = note.PapyruscVersion,
            CkCompilerPathSha256 = note.CkCompilerPathSha256,
            CkFileVersion = note.CkFileVersion,
            Statement = note.Statement,
        };
    }
}
