using System;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PexReaderCapTests
{
    [Fact]
    public void Caps_MatchDesignDoc()
    {
        Assert.Equal(1_048_576, PexLimits.MaxStringTableEntries);
        Assert.Equal(4_096, PexLimits.MaxObjects);
        Assert.Equal(8_192, PexLimits.MaxFunctionsPerObject);
        Assert.Equal(1_048_576, PexLimits.MaxInstructionsPerFunction);
        Assert.Equal(8_192, PexLimits.MaxDebugInfoFunctions);
    }

    [Fact]
    public void StringTable_AboveCap_Throws()
    {
        // On-disk count is u16 so a real file cannot exceed 65,535; the reader
        // still funnels the count through Ensure so the cap is a hard error.
        PexFormatException ex = Assert.Throws<PexFormatException>(
            () => PexLimits.Ensure(PexLimits.MaxStringTableEntries + 1, PexLimits.MaxStringTableEntries, "string table"));
        Assert.Contains("string table", ex.Message, StringComparison.OrdinalIgnoreCase);

        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        Assert.Empty(file.StringTable);
        PexLimits.Ensure(file.StringTable.Length, PexLimits.MaxStringTableEntries, "string table");
    }

    [Fact]
    public void Objects_AboveCap_ThrowsWithoutReadingBodies()
    {
        ushort count = (ushort)(PexLimits.MaxObjects + 1);
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(0)
            .U8(0)
            .U16(0)
            .U16(count)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("objects", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PexLimits.MaxObjects.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionsPerObject_AboveCap_Throws()
    {
        ushort count = (ushort)(PexLimits.MaxFunctionsPerObject + 1);
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(2)
            .Str("Script")
            .Str("")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1)
            .U32(0)
            .U16(1)
            .U16(0) // vars
            .U16(0) // props
            .U16(1) // one state
            .U16(1)
            .U16(count)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("functions per object", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PexLimits.MaxFunctionsPerObject.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionsPerObject_CountsPropertyAccessors()
    {
        // 8192 state functions already at the cap; a getter must fail.
        ushort stateFunctions = (ushort)PexLimits.MaxFunctionsPerObject;
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(4)
            .Str("Script")
            .Str("")
            .Str("Prop")
            .Str("Int")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1)
            .U32(0)
            .U16(1)
            .U16(0)
            .U16(1) // one readable property → +1 function
            .U16(2)
            .U16(3)
            .U16(1)
            .U32(0)
            .U8(0x01)
            .U16(1) // getter return
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(0)
            .U16(1) // one state
            .U16(1)
            .U16(stateFunctions)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("functions per object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstructionsPerFunction_AboveCap_IsHardError()
    {
        // Instruction count is a u16, so a fixture cannot declare more than 65,535.
        PexFormatException ex = Assert.Throws<PexFormatException>(
            () => PexLimits.Ensure(
                PexLimits.MaxInstructionsPerFunction + 1,
                PexLimits.MaxInstructionsPerFunction,
                "instructions per function"));
        Assert.Contains("instructions per function", ex.Message, StringComparison.OrdinalIgnoreCase);

        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(3)
            .Str("Script")
            .Str("")
            .Str("Fn")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1)
            .U32(0)
            .U16(1)
            .U16(0)
            .U16(0)
            .U16(1)
            .U16(1)
            .U16(1)
            .U16(2)
            .U16(1)
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(0)
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        Assert.Empty(file.Objects[0].States[0].Functions[0].Instructions);
        PexLimits.Ensure(
            file.Objects[0].States[0].Functions[0].Instructions.Length,
            PexLimits.MaxInstructionsPerFunction,
            "instructions per function");
    }

    [Fact]
    public void DebugInfoFunctions_AboveCap_Throws()
    {
        ushort count = (ushort)(PexLimits.MaxDebugInfoFunctions + 1);
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(0)
            .U8(1)
            .U64(0)
            .U16(count)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("debug-info functions", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PexLimits.MaxDebugInfoFunctions.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LengthPrefixedBlob_ExceedingRemainingStream_Throws()
    {
        byte[] bytes = new PexBlobBuilder()
            .MagicLe()
            .Version(PexVersion.V32)
            .U16(GameIdMapping.SkyrimRaw)
            .U64(0)
            .U16(1000)
            .Bytes(0x41)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("exceeds the remaining", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PexLimits.MaxStringTableEntries + 1, PexLimits.MaxStringTableEntries, "string table")]
    [InlineData(PexLimits.MaxObjects + 1, PexLimits.MaxObjects, "objects")]
    [InlineData(PexLimits.MaxFunctionsPerObject + 1, PexLimits.MaxFunctionsPerObject, "functions per object")]
    [InlineData(PexLimits.MaxInstructionsPerFunction + 1, PexLimits.MaxInstructionsPerFunction, "instructions per function")]
    [InlineData(PexLimits.MaxDebugInfoFunctions + 1, PexLimits.MaxDebugInfoFunctions, "debug-info functions")]
    public void Ensure_EachCap_IsAHardError(int count, int max, string what)
    {
        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexLimits.Ensure(count, max, what));
        Assert.Contains(what, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(max.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_AtCap_DoesNotThrow()
    {
        PexLimits.Ensure(PexLimits.MaxStringTableEntries, PexLimits.MaxStringTableEntries, "string table");
        PexLimits.Ensure(PexLimits.MaxObjects, PexLimits.MaxObjects, "objects");
        PexLimits.Ensure(PexLimits.MaxFunctionsPerObject, PexLimits.MaxFunctionsPerObject, "functions per object");
        PexLimits.Ensure(PexLimits.MaxInstructionsPerFunction, PexLimits.MaxInstructionsPerFunction, "instructions per function");
        PexLimits.Ensure(PexLimits.MaxDebugInfoFunctions, PexLimits.MaxDebugInfoFunctions, "debug-info functions");
    }
}
