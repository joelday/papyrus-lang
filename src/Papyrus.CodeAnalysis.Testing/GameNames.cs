using System;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Testing;

public static class GameNames
{
    public static bool TryParse(string? value, out string canonical, out GameId game)
    {
        canonical = null!;
        game = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (string.Equals(value, "skyrimse", StringComparison.OrdinalIgnoreCase))
        {
            canonical = "skyrimse";
            game = GameId.SkyrimSpecialEdition;
            return true;
        }

        if (string.Equals(value, "skyrimle", StringComparison.OrdinalIgnoreCase))
        {
            canonical = "skyrimle";
            game = GameId.Skyrim;
            return true;
        }

        if (string.Equals(value, "fallout4", StringComparison.OrdinalIgnoreCase))
        {
            canonical = "fallout4";
            game = GameId.Fallout4;
            return true;
        }

        if (string.Equals(value, "starfield", StringComparison.OrdinalIgnoreCase))
        {
            canonical = "starfield";
            game = GameId.Starfield;
            return true;
        }

        if (string.Equals(value, "synthetic", StringComparison.OrdinalIgnoreCase))
        {
            canonical = "synthetic";
            game = GameId.SkyrimSpecialEdition;
            return true;
        }

        return false;
    }
}
