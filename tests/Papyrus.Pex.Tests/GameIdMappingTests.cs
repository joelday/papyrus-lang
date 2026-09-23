using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class GameIdMappingTests
{
    [Theory]
    [InlineData((ushort)0, PexVersion.V32)]
    [InlineData((ushort)0xFFFF, PexVersion.V32)]
    [InlineData((ushort)5, PexVersion.V312)]
    [InlineData((ushort)99, PexVersion.V39)]
    public void Resolve_UnknownGameId_IsUnresolved(ushort pexGameId, PexVersion version)
    {
        Assert.Null(GameIdMapping.Resolve(pexGameId, version));
    }

    [Fact]
    public void Resolve_SkyrimV31_IsSkyrim()
    {
        Assert.Equal(GameId.Skyrim, GameIdMapping.Resolve(GameIdMapping.SkyrimRaw, PexVersion.V31));
    }

    [Fact]
    public void Resolve_SkyrimV32_DefaultsToSpecialEdition()
    {
        Assert.Equal(GameId.SkyrimSpecialEdition, GameIdMapping.Resolve(GameIdMapping.SkyrimRaw, PexVersion.V32));
    }

    [Fact]
    public void Resolve_SkyrimV32_CanForceLegacySkyrim()
    {
        Assert.Equal(GameId.Skyrim, GameIdMapping.Resolve(GameIdMapping.SkyrimRaw, PexVersion.V32, GameId.Skyrim));
    }

    [Fact]
    public void Resolve_Fallout4V39()
    {
        Assert.Equal(GameId.Fallout4, GameIdMapping.Resolve(GameIdMapping.Fallout4Raw, PexVersion.V39));
    }

    [Fact]
    public void Resolve_Fallout76V315()
    {
        Assert.Equal(GameId.Fallout76, GameIdMapping.Resolve(GameIdMapping.Fallout76Raw, PexVersion.V315));
    }

    [Fact]
    public void Resolve_StarfieldV312()
    {
        Assert.Equal(GameId.Starfield, GameIdMapping.Resolve(GameIdMapping.StarfieldRaw, PexVersion.V312));
    }

    [Theory]
    [InlineData(GameId.Skyrim, (ushort)1)]
    [InlineData(GameId.SkyrimSpecialEdition, (ushort)1)]
    [InlineData(GameId.Fallout4, (ushort)2)]
    [InlineData(GameId.Fallout76, (ushort)3)]
    [InlineData(GameId.Starfield, (ushort)4)]
    public void ToRaw_MatchesCapricaGameId(GameId game, ushort expected)
    {
        Assert.Equal(expected, GameIdMapping.ToRaw(game));
    }

    [Theory]
    [InlineData(GameId.Skyrim, PexOpCode.ArrayRFindElement)]
    [InlineData(GameId.SkyrimSpecialEdition, PexOpCode.ArrayRFindElement)]
    [InlineData(GameId.Fallout4, PexOpCode.ArrayClear)]
    [InlineData(GameId.Fallout76, PexOpCode.ArrayGetAllMatchingStructs)]
    [InlineData(GameId.Starfield, PexOpCode.TryLockGuards)]
    public void GetMaxOpCode_MatchesDialect(GameId game, PexOpCode expected)
    {
        Assert.Equal(expected, GameIdMapping.GetMaxOpCode(game));
    }
}
