using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis;

public sealed class PapyrusParseOptions
{
    public static PapyrusParseOptions Default { get; } = new();

    public PapyrusEncoding Encoding { get; init; } = PapyrusEncoding.Windows1252;

    public bool ChampollionCompat { get; init; }

    public bool Fo4Plus { get; init; }

    public bool Starfield { get; init; }

    public bool IgnoreEngineLimits { get; init; }
}
