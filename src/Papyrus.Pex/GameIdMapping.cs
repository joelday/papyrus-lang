namespace Papyrus.Pex;

/// <summary>
/// Maps the PEX header <c>gameId:u16</c> plus <see cref="PexVersion"/> onto <see cref="GameId"/>.
/// Caprica <c>GameID.h</c> at e4dee08: Skyrim=1, Fallout4=2, Fallout76=3, Starfield=4.
/// </summary>
public static class GameIdMapping
{
    public const ushort UnknownRaw = 0;
    public const ushort InvalidRaw = 0xFFFF;
    public const ushort SkyrimRaw = 1;
    public const ushort Fallout4Raw = 2;
    public const ushort Fallout76Raw = 3;
    public const ushort StarfieldRaw = 4;

    /// <summary>
    /// Resolves a PEX header game id. Returns <see langword="null"/> when the header
    /// cannot identify a game (0, 0xFFFF, or any value outside 1–4).
    /// V32 Skyrim files default to <see cref="GameId.SkyrimSpecialEdition"/>;
    /// pass <paramref name="preferredGame"/> as <see cref="GameId.Skyrim"/> to force LE.
    /// </summary>
    public static GameId? Resolve(ushort pexGameId, PexVersion version, GameId? preferredGame = null)
    {
        switch (pexGameId)
        {
            case SkyrimRaw:
                if (version == PexVersion.V31)
                    return GameId.Skyrim;

                // V32 cannot tell LE expansions from SE; SE is the compile target.
                if (preferredGame == GameId.Skyrim)
                    return GameId.Skyrim;

                return GameId.SkyrimSpecialEdition;

            case Fallout4Raw:
                return GameId.Fallout4;

            case Fallout76Raw:
                return GameId.Fallout76;

            case StarfieldRaw:
                return GameId.Starfield;

            default:
                return null;
        }
    }

    public static ushort ToRaw(GameId game)
    {
        return game switch
        {
            GameId.Skyrim or GameId.SkyrimSpecialEdition => SkyrimRaw,
            GameId.Fallout4 => Fallout4Raw,
            GameId.Fallout76 => Fallout76Raw,
            GameId.Starfield => StarfieldRaw,
            _ => UnknownRaw,
        };
    }

    public static PexOpCode GetMaxOpCode(GameId game)
    {
        return game switch
        {
            GameId.Skyrim or GameId.SkyrimSpecialEdition => PexOpCode.SkyrimMax,
            GameId.Fallout4 => PexOpCode.Fallout4Max,
            GameId.Fallout76 => PexOpCode.Fallout76Max,
            GameId.Starfield => PexOpCode.StarfieldMax,
            _ => PexOpCode.Max,
        };
    }
}
