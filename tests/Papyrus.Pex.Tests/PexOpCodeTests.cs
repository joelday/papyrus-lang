using System;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PexOpCodeTests
{
    [Fact]
    public void ArrayGetAllMatchingStructs_Exists()
    {
        Assert.True(Enum.IsDefined(PexOpCode.ArrayGetAllMatchingStructs));
        Assert.Equal(47, (byte)PexOpCode.ArrayGetAllMatchingStructs);
    }

    [Theory]
    [InlineData(PexOpCode.Nop, 0)]
    [InlineData(PexOpCode.IAdd, 1)]
    [InlineData(PexOpCode.Assign, 13)]
    [InlineData(PexOpCode.Cast, 14)]
    [InlineData(PexOpCode.Return, 26)]
    [InlineData(PexOpCode.ArrayRFindElement, 35)]
    [InlineData(PexOpCode.Is, 36)]
    [InlineData(PexOpCode.ArrayClear, 46)]
    [InlineData(PexOpCode.ArrayGetAllMatchingStructs, 47)]
    [InlineData(PexOpCode.LockGuards, 48)]
    [InlineData(PexOpCode.UnlockGuards, 49)]
    [InlineData(PexOpCode.TryLockGuards, 50)]
    [InlineData(PexOpCode.Invalid, 0xFF)]
    public void Opcode_MatchesCapricaValue(PexOpCode op, byte expected)
    {
        Assert.Equal(expected, (byte)op);
    }

    [Fact]
    public void Opcodes_AreSequentialThroughStarfield()
    {
        PexOpCode[] sequential =
        [
            PexOpCode.Nop,
            PexOpCode.IAdd,
            PexOpCode.FAdd,
            PexOpCode.ISub,
            PexOpCode.FSub,
            PexOpCode.IMul,
            PexOpCode.FMul,
            PexOpCode.IDiv,
            PexOpCode.FDiv,
            PexOpCode.IMod,
            PexOpCode.Not,
            PexOpCode.INeg,
            PexOpCode.FNeg,
            PexOpCode.Assign,
            PexOpCode.Cast,
            PexOpCode.CmpEq,
            PexOpCode.CmpLt,
            PexOpCode.CmpLte,
            PexOpCode.CmpGt,
            PexOpCode.CmpGte,
            PexOpCode.Jmp,
            PexOpCode.JmpT,
            PexOpCode.JmpF,
            PexOpCode.CallMethod,
            PexOpCode.CallParent,
            PexOpCode.CallStatic,
            PexOpCode.Return,
            PexOpCode.StrCat,
            PexOpCode.PropGet,
            PexOpCode.PropSet,
            PexOpCode.ArrayCreate,
            PexOpCode.ArrayLength,
            PexOpCode.ArrayGetElement,
            PexOpCode.ArraySetElement,
            PexOpCode.ArrayFindElement,
            PexOpCode.ArrayRFindElement,
            PexOpCode.Is,
            PexOpCode.StructCreate,
            PexOpCode.StructGet,
            PexOpCode.StructSet,
            PexOpCode.ArrayFindStruct,
            PexOpCode.ArrayRFindStruct,
            PexOpCode.ArrayAdd,
            PexOpCode.ArrayInsert,
            PexOpCode.ArrayRemoveLast,
            PexOpCode.ArrayRemove,
            PexOpCode.ArrayClear,
            PexOpCode.ArrayGetAllMatchingStructs,
            PexOpCode.LockGuards,
            PexOpCode.UnlockGuards,
            PexOpCode.TryLockGuards,
        ];

        Assert.Equal(51, sequential.Length);
        for (int i = 0; i < sequential.Length; i++)
            Assert.Equal(i, (byte)sequential[i]);
    }

    [Fact]
    public void DialectMaxAliases_MatchLastOpcode()
    {
        Assert.Equal(PexOpCode.ArrayRFindElement, PexOpCode.SkyrimMax);
        Assert.Equal(PexOpCode.ArrayClear, PexOpCode.Fallout4Max);
        Assert.Equal(PexOpCode.ArrayGetAllMatchingStructs, PexOpCode.Fallout76Max);
        Assert.Equal(PexOpCode.TryLockGuards, PexOpCode.StarfieldMax);
        Assert.Equal(PexOpCode.TryLockGuards, PexOpCode.Max);
    }

    [Fact]
    public void PexVersion_MatchesHeaderWords()
    {
        Assert.Equal(0x0301, (ushort)PexVersion.V31);
        Assert.Equal(0x0302, (ushort)PexVersion.V32);
        Assert.Equal(0x0309, (ushort)PexVersion.V39);
        Assert.Equal(0x030C, (ushort)PexVersion.V312);
        Assert.Equal(0x030F, (ushort)PexVersion.V315);
        Assert.Equal(3, PexVersionInfo.GetMajor(PexVersion.V312));
        Assert.Equal(12, PexVersionInfo.GetMinor(PexVersion.V312));
        Assert.Equal(PexVersion.V39, PexVersionInfo.FromParts(3, 9));
    }
}
