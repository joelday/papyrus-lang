using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PexWriterTests
{
    [Fact]
    public void Write_Read_MinimalSkyrim_BytesAreIdentical()
    {
        byte[] original = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw, compileTime: 1_700_000_000)
            .EmptyTail()
            .ToArray();

        AssertBytesRoundTrip(original);
    }

    [Fact]
    public void Write_Read_SkyrimScriptWithNop_BytesAreIdentical()
    {
        AssertBytesRoundTrip(BuildSkyrimScriptWithNop());
    }

    [Fact]
    public void Write_Read_Fallout4ConstAndStructs_BytesAreIdentical()
    {
        AssertBytesRoundTrip(BuildFallout4ConstAndStructs());
    }

    [Fact]
    public void Write_Read_Fallout76Opcode_BytesAreIdentical()
    {
        byte[] original = BuildInstructionFile(
            PexVersion.V315,
            GameIdMapping.Fallout76Raw,
            extended: true,
            opcode: PexOpCode.ArrayGetAllMatchingStructs,
            args: b => b
                .ValueIdent(3)
                .ValueIdent(3)
                .ValueString(2)
                .ValueInt(0)
                .ValueInt(0)
                .ValueInt(1));

        AssertBytesRoundTrip(original);
    }

    [Fact]
    public void Write_Read_StarfieldGuards_BytesAreIdentical()
    {
        AssertBytesRoundTrip(BuildStarfieldGuards());
    }

    [Fact]
    public void Write_Read_CallMethodVariadic_BytesAreIdentical()
    {
        byte[] original = BuildInstructionFile(
            PexVersion.V32,
            GameIdMapping.SkyrimRaw,
            extended: false,
            opcode: PexOpCode.CallMethod,
            args: b => b
                .ValueString(2)
                .ValueIdent(0)
                .ValueIdent(3)
                .ValueInt(2)
                .ValueInt(1)
                .ValueIdent(3));

        AssertBytesRoundTrip(original);
    }

    [Fact]
    public void Write_Read_IAdd_BytesAreIdentical()
    {
        byte[] original = BuildInstructionFile(
            PexVersion.V32,
            GameIdMapping.SkyrimRaw,
            extended: false,
            opcode: PexOpCode.IAdd,
            args: b => b.ValueIdent(3).ValueInt(1).ValueInt(2));

        AssertBytesRoundTrip(original);
    }

    [Fact]
    public void Write_Read_PropertyGetter_BytesAreIdentical()
    {
        AssertBytesRoundTrip(BuildPropertyWithGetter());
    }

    [Fact]
    public void Write_ConstructedFile_ReadIsStructurallyEqual()
    {
        PexFile original = BuildConstructedSkyrimFile();

        byte[] bytes = PexWriter.Write(original);
        PexFile roundTripped = PexReader.Read(bytes);

        AssertStructurallyEqual(original, roundTripped);
        Assert.Equal(bytes, PexWriter.Write(roundTripped));
    }

    [Fact]
    public void Write_ConstructedFallout4_PreservesExtendedLayout()
    {
        PexFile original = BuildConstructedFallout4File();

        byte[] bytes = PexWriter.Write(original);
        PexFile roundTripped = PexReader.Read(bytes);

        AssertStructurallyEqual(original, roundTripped);
        Assert.Equal(bytes, PexWriter.Write(roundTripped));
    }

    [Fact]
    public void Write_DoesNotUseWallClock()
    {
        var file = new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
        };

        byte[] bytes = PexWriter.Write(file);
        PexFile read = PexReader.Read(bytes);

        Assert.Equal(DateTimeOffset.UnixEpoch, read.CompileTime);
        Assert.Null(read.DebugInfo);
        Assert.Equal(bytes, PexWriter.Write(read));
    }

    [Fact]
    public void Write_UsesCompileTimeFromFile()
    {
        var compileTime = DateTimeOffset.FromUnixTimeSeconds(1_234_567);
        var file = new PexFile
        {
            Version = PexVersion.V31,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.Skyrim,
            CompileTime = compileTime,
            SourcePath = "A.psc",
            UserName = "u",
            MachineName = "m",
        };

        PexFile read = PexReader.Read(PexWriter.Write(file));
        Assert.Equal(compileTime, read.CompileTime);
        Assert.Equal("A.psc", read.SourcePath);
        Assert.Equal("u", read.UserName);
        Assert.Equal("m", read.MachineName);
    }

    [Fact]
    public void Write_InternsMissingStringTableEntries()
    {
        var file = new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    ParentClassName = "",
                    DocumentationString = "",
                    AutoStateName = "",
                },
            ],
        };

        PexFile read = PexReader.Read(PexWriter.Write(file));
        Assert.Contains("Script", read.StringTable);
        Assert.Contains("", read.StringTable);
        Assert.Equal("Script", Assert.Single(read.Objects).Name);
    }

    [Fact]
    public void Write_StreamAndArray_Match()
    {
        PexFile file = BuildConstructedSkyrimFile();
        byte[] fromArray = PexWriter.Write(file);

        using var stream = new MemoryStream();
        PexWriter.Write(file, stream);
        Assert.Equal(fromArray, stream.ToArray());
    }

    [Fact]
    public void Write_Twice_IsDeterministic()
    {
        PexFile file = BuildConstructedSkyrimFile();
        Assert.Equal(PexWriter.Write(file), PexWriter.Write(file));
    }

    [Fact]
    public void Write_NullFile_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PexWriter.Write((PexFile)null!));
        Assert.Throws<ArgumentNullException>(() => PexWriter.Write(null!, Stream.Null));
    }

    [Fact]
    public void Write_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PexWriter.Write(new PexFile(), null!));
    }

    [Fact]
    public void Write_ReadOnlyStream_Throws()
    {
        using var stream = new MemoryStream(new byte[16], writable: false);
        Assert.Throws<ArgumentException>(() => PexWriter.Write(new PexFile { Version = PexVersion.V32 }, stream));
    }

    [Fact]
    public void Write_EmitsLittleEndianMagic()
    {
        byte[] bytes = PexWriter.Write(new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
        });

        Assert.Equal(new byte[] { 0xDE, 0xC0, 0x57, 0xFA }, bytes[..4]);
    }

    [Fact]
    public void Write_Read_Write_IsIdempotentOnReaderFixtures()
    {
        foreach (byte[] original in ReaderFixtures())
        {
            byte[] once = PexWriter.Write(PexReader.Read(original));
            byte[] twice = PexWriter.Write(PexReader.Read(once));
            Assert.Equal(once, twice);
        }
    }

    private static void AssertBytesRoundTrip(byte[] original)
    {
        PexFile file = PexReader.Read(original);
        byte[] written = PexWriter.Write(file);
        Assert.Equal(original, written);

        PexFile again = PexReader.Read(written);
        AssertStructurallyEqual(file, again);
    }

    private static byte[][] ReaderFixtures()
    {
        return
        [
            new PexBlobBuilder()
                .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
                .EmptyTail()
                .ToArray(),
            BuildSkyrimScriptWithNop(),
            BuildFallout4ConstAndStructs(),
            BuildStarfieldGuards(),
            BuildPropertyWithGetter(),
            BuildInstructionFile(
                PexVersion.V32,
                GameIdMapping.SkyrimRaw,
                extended: false,
                opcode: PexOpCode.IAdd,
                args: b => b.ValueIdent(3).ValueInt(1).ValueInt(2)),
        ];
    }

    private static PexFile BuildConstructedSkyrimFile()
    {
        return new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            CompileTime = DateTimeOffset.FromUnixTimeSeconds(99),
            SourcePath = "Built.psc",
            UserName = "builder",
            MachineName = "box",
            StringTable = ["Built", "", "docs", "Int", "Go", "None", "Hidden", "::tmp"],
            DebugInfo = new PexDebugInfo
            {
                ModificationTime = DateTimeOffset.FromUnixTimeSeconds(7),
                Functions =
                [
                    new PexDebugFunction
                    {
                        ObjectName = "Built",
                        StateName = "",
                        FunctionName = "Go",
                        FunctionType = PexDebugFunctionType.Normal,
                        InstructionLineMap = [12, 13],
                    },
                ],
            },
            UserFlags =
            [
                new PexUserFlag { Name = "Hidden", BitIndex = 0 },
            ],
            Objects =
            [
                new PexObject
                {
                    Name = "Built",
                    Size = 0,
                    ParentClassName = "",
                    DocumentationString = "docs",
                    UserFlags = 1,
                    AutoStateName = "",
                    Variables =
                    [
                        new PexVariable
                        {
                            Name = "::tmp",
                            TypeName = "Int",
                            DefaultValue = new PexValue { Type = PexValueType.Integer, Integer = 4 },
                        },
                    ],
                    States =
                    [
                        new PexState
                        {
                            Name = "",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "Go",
                                    ReturnTypeName = "None",
                                    DocumentationString = "",
                                    Parameters =
                                    [
                                        new PexFunctionParameter { Name = "::tmp", TypeName = "Int" },
                                    ],
                                    Locals =
                                    [
                                        new PexLocalVariable { Name = "::tmp", TypeName = "Int" },
                                    ],
                                    Instructions =
                                    [
                                        new PexInstruction
                                        {
                                            OpCode = PexOpCode.IAdd,
                                            Arguments =
                                            [
                                                new PexValue { Type = PexValueType.Identifier, Text = "::tmp" },
                                                new PexValue { Type = PexValueType.Integer, Integer = 1 },
                                                new PexValue { Type = PexValueType.Integer, Integer = 2 },
                                            ],
                                        },
                                        new PexInstruction
                                        {
                                            OpCode = PexOpCode.Return,
                                            Arguments = [PexValue.None],
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }

    private static PexFile BuildConstructedFallout4File()
    {
        return new PexFile
        {
            Version = PexVersion.V39,
            RawGameId = GameIdMapping.Fallout4Raw,
            Game = GameId.Fallout4,
            CompileTime = DateTimeOffset.UnixEpoch,
            SourcePath = "Foo.psc",
            UserName = "u",
            MachineName = "h",
            StringTable = ["Foo", "Bar", "", "Int", "Member", "Prop", "Docs", "::auto"],
            DebugInfo = new PexDebugInfo
            {
                ModificationTime = DateTimeOffset.FromUnixTimeSeconds(1),
                Functions =
                [
                    new PexDebugFunction
                    {
                        ObjectName = "Foo",
                        StateName = "",
                        FunctionName = "Prop",
                        FunctionType = PexDebugFunctionType.Getter,
                        InstructionLineMap = [3],
                    },
                ],
                PropertyGroups =
                [
                    new PexDebugPropertyGroup
                    {
                        ObjectName = "Foo",
                        GroupName = "Bar",
                        DocumentationString = "Docs",
                        UserFlags = 0,
                        Properties = ["Prop"],
                    },
                ],
                StructOrders =
                [
                    new PexDebugStructOrder
                    {
                        ObjectName = "Foo",
                        StructName = "Bar",
                        Members = ["Member"],
                    },
                ],
            },
            Objects =
            [
                new PexObject
                {
                    Name = "Foo",
                    ParentClassName = "",
                    DocumentationString = "",
                    IsConst = true,
                    AutoStateName = "",
                    Structs =
                    [
                        new PexStruct
                        {
                            Name = "Bar",
                            Members =
                            [
                                new PexStructMember
                                {
                                    Name = "Member",
                                    TypeName = "Int",
                                    DefaultValue = new PexValue { Type = PexValueType.Integer, Integer = 3 },
                                    IsConst = true,
                                    DocumentationString = "",
                                },
                            ],
                        },
                    ],
                    Variables =
                    [
                        new PexVariable
                        {
                            Name = "::auto",
                            TypeName = "Int",
                            IsConst = true,
                            DefaultValue = new PexValue { Type = PexValueType.Bool, Bool = true },
                        },
                    ],
                    Properties =
                    [
                        new PexProperty
                        {
                            Name = "Prop",
                            TypeName = "Int",
                            DocumentationString = "Docs",
                            IsAuto = true,
                            IsReadable = true,
                            IsWritable = true,
                            AutoVar = "::auto",
                        },
                    ],
                },
            ],
        };
    }

    private static void AssertStructurallyEqual(PexFile expected, PexFile actual)
    {
        Assert.Equal(expected.Magic, actual.Magic);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.RawGameId, actual.RawGameId);
        Assert.Equal(expected.Game, actual.Game);
        Assert.Equal(expected.CompileTime, actual.CompileTime);
        Assert.Equal(expected.SourcePath, actual.SourcePath);
        Assert.Equal(expected.UserName, actual.UserName);
        Assert.Equal(expected.MachineName, actual.MachineName);
        AssertSequence(expected.StringTable, actual.StringTable);

        Assert.Equal(expected.DebugInfo is null, actual.DebugInfo is null);
        if (expected.DebugInfo is not null && actual.DebugInfo is not null)
        {
            Assert.Equal(expected.DebugInfo.ModificationTime, actual.DebugInfo.ModificationTime);
            Assert.Equal(expected.DebugInfo.Functions.Length, actual.DebugInfo.Functions.Length);
            for (int i = 0; i < expected.DebugInfo.Functions.Length; i++)
            {
                PexDebugFunction e = expected.DebugInfo.Functions[i];
                PexDebugFunction a = actual.DebugInfo.Functions[i];
                Assert.Equal(e.ObjectName, a.ObjectName);
                Assert.Equal(e.StateName, a.StateName);
                Assert.Equal(e.FunctionName, a.FunctionName);
                Assert.Equal(e.FunctionType, a.FunctionType);
                AssertSequence(e.InstructionLineMap, a.InstructionLineMap);
            }

            Assert.Equal(expected.DebugInfo.PropertyGroups.Length, actual.DebugInfo.PropertyGroups.Length);
            for (int i = 0; i < expected.DebugInfo.PropertyGroups.Length; i++)
            {
                PexDebugPropertyGroup e = expected.DebugInfo.PropertyGroups[i];
                PexDebugPropertyGroup a = actual.DebugInfo.PropertyGroups[i];
                Assert.Equal(e.ObjectName, a.ObjectName);
                Assert.Equal(e.GroupName, a.GroupName);
                Assert.Equal(e.DocumentationString, a.DocumentationString);
                Assert.Equal(e.UserFlags, a.UserFlags);
                AssertSequence(e.Properties, a.Properties);
            }

            Assert.Equal(expected.DebugInfo.StructOrders.Length, actual.DebugInfo.StructOrders.Length);
            for (int i = 0; i < expected.DebugInfo.StructOrders.Length; i++)
            {
                PexDebugStructOrder e = expected.DebugInfo.StructOrders[i];
                PexDebugStructOrder a = actual.DebugInfo.StructOrders[i];
                Assert.Equal(e.ObjectName, a.ObjectName);
                Assert.Equal(e.StructName, a.StructName);
                AssertSequence(e.Members, a.Members);
            }
        }

        Assert.Equal(expected.UserFlags.Length, actual.UserFlags.Length);
        for (int i = 0; i < expected.UserFlags.Length; i++)
        {
            Assert.Equal(expected.UserFlags[i].Name, actual.UserFlags[i].Name);
            Assert.Equal(expected.UserFlags[i].BitIndex, actual.UserFlags[i].BitIndex);
        }

        Assert.Equal(expected.Objects.Length, actual.Objects.Length);
        for (int i = 0; i < expected.Objects.Length; i++)
            AssertObjectEqual(expected.Objects[i], actual.Objects[i]);
    }

    private static void AssertObjectEqual(PexObject expected, PexObject actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.ParentClassName, actual.ParentClassName);
        Assert.Equal(expected.DocumentationString, actual.DocumentationString);
        Assert.Equal(expected.IsConst, actual.IsConst);
        Assert.Equal(expected.UserFlags, actual.UserFlags);
        Assert.Equal(expected.AutoStateName, actual.AutoStateName);
        Assert.Equal(expected.Guards.Length, actual.Guards.Length);
        for (int i = 0; i < expected.Guards.Length; i++)
            Assert.Equal(expected.Guards[i].Name, actual.Guards[i].Name);

        Assert.Equal(expected.Structs.Length, actual.Structs.Length);
        for (int i = 0; i < expected.Structs.Length; i++)
        {
            Assert.Equal(expected.Structs[i].Name, actual.Structs[i].Name);
            Assert.Equal(expected.Structs[i].Members.Length, actual.Structs[i].Members.Length);
            for (int m = 0; m < expected.Structs[i].Members.Length; m++)
            {
                PexStructMember e = expected.Structs[i].Members[m];
                PexStructMember a = actual.Structs[i].Members[m];
                Assert.Equal(e.Name, a.Name);
                Assert.Equal(e.TypeName, a.TypeName);
                Assert.Equal(e.UserFlags, a.UserFlags);
                Assert.Equal(e.IsConst, a.IsConst);
                Assert.Equal(e.DocumentationString, a.DocumentationString);
                AssertValueEqual(e.DefaultValue, a.DefaultValue);
            }
        }

        Assert.Equal(expected.Variables.Length, actual.Variables.Length);
        for (int i = 0; i < expected.Variables.Length; i++)
        {
            Assert.Equal(expected.Variables[i].Name, actual.Variables[i].Name);
            Assert.Equal(expected.Variables[i].TypeName, actual.Variables[i].TypeName);
            Assert.Equal(expected.Variables[i].UserFlags, actual.Variables[i].UserFlags);
            Assert.Equal(expected.Variables[i].IsConst, actual.Variables[i].IsConst);
            AssertValueEqual(expected.Variables[i].DefaultValue, actual.Variables[i].DefaultValue);
        }

        Assert.Equal(expected.Properties.Length, actual.Properties.Length);
        for (int i = 0; i < expected.Properties.Length; i++)
        {
            PexProperty e = expected.Properties[i];
            PexProperty a = actual.Properties[i];
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.TypeName, a.TypeName);
            Assert.Equal(e.DocumentationString, a.DocumentationString);
            Assert.Equal(e.UserFlags, a.UserFlags);
            Assert.Equal(e.IsAuto, a.IsAuto);
            Assert.Equal(e.IsReadable, a.IsReadable);
            Assert.Equal(e.IsWritable, a.IsWritable);
            Assert.Equal(e.AutoVar, a.AutoVar);
            Assert.Equal(e.ReadFunction is null, a.ReadFunction is null);
            if (e.ReadFunction is not null && a.ReadFunction is not null)
                AssertFunctionEqual(e.ReadFunction, a.ReadFunction);
            Assert.Equal(e.WriteFunction is null, a.WriteFunction is null);
            if (e.WriteFunction is not null && a.WriteFunction is not null)
                AssertFunctionEqual(e.WriteFunction, a.WriteFunction);
        }

        Assert.Equal(expected.States.Length, actual.States.Length);
        for (int i = 0; i < expected.States.Length; i++)
        {
            Assert.Equal(expected.States[i].Name, actual.States[i].Name);
            Assert.Equal(expected.States[i].Functions.Length, actual.States[i].Functions.Length);
            for (int f = 0; f < expected.States[i].Functions.Length; f++)
                AssertFunctionEqual(expected.States[i].Functions[f], actual.States[i].Functions[f]);
        }
    }

    private static void AssertFunctionEqual(PexFunction expected, PexFunction actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.ReturnTypeName, actual.ReturnTypeName);
        Assert.Equal(expected.DocumentationString, actual.DocumentationString);
        Assert.Equal(expected.UserFlags, actual.UserFlags);
        Assert.Equal(expected.IsGlobal, actual.IsGlobal);
        Assert.Equal(expected.IsNative, actual.IsNative);
        Assert.Equal(expected.Parameters.Length, actual.Parameters.Length);
        for (int i = 0; i < expected.Parameters.Length; i++)
        {
            Assert.Equal(expected.Parameters[i].Name, actual.Parameters[i].Name);
            Assert.Equal(expected.Parameters[i].TypeName, actual.Parameters[i].TypeName);
        }

        Assert.Equal(expected.Locals.Length, actual.Locals.Length);
        for (int i = 0; i < expected.Locals.Length; i++)
        {
            Assert.Equal(expected.Locals[i].Name, actual.Locals[i].Name);
            Assert.Equal(expected.Locals[i].TypeName, actual.Locals[i].TypeName);
        }

        Assert.Equal(expected.Instructions.Length, actual.Instructions.Length);
        for (int i = 0; i < expected.Instructions.Length; i++)
        {
            Assert.Equal(expected.Instructions[i].OpCode, actual.Instructions[i].OpCode);
            Assert.Equal(expected.Instructions[i].Arguments.Length, actual.Instructions[i].Arguments.Length);
            for (int a = 0; a < expected.Instructions[i].Arguments.Length; a++)
                AssertValueEqual(expected.Instructions[i].Arguments[a], actual.Instructions[i].Arguments[a]);
            Assert.Equal(expected.Instructions[i].VariadicArguments.Length, actual.Instructions[i].VariadicArguments.Length);
            for (int a = 0; a < expected.Instructions[i].VariadicArguments.Length; a++)
                AssertValueEqual(expected.Instructions[i].VariadicArguments[a], actual.Instructions[i].VariadicArguments[a]);
        }
    }

    // ImmutableArray.Equals is reference equality on the backing array.
    private static void AssertSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual) =>
        Assert.Equal(expected.ToArray(), actual.ToArray());

    private static void AssertValueEqual(PexValue expected, PexValue actual)
    {
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Equal(expected.Integer, actual.Integer);
        Assert.Equal(expected.Float, actual.Float);
        Assert.Equal(expected.Bool, actual.Bool);
    }

    private static byte[] BuildSkyrimScriptWithNop()
    {
        return new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(8)
            .Str("MyScript")
            .Str("")
            .Str("docs")
            .Str("Int")
            .Str("MyFunc")
            .Str("None")
            .Str("::temp")
            .Str("Hidden")
            .U8(1)
            .U64(42)
            .U16(1)
            .U16(0)
            .U16(1)
            .U16(4)
            .U8((byte)PexDebugFunctionType.Normal)
            .U16(1)
            .U16(10)
            .U16(1)
            .U16(7)
            .U8(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(2)
            .U32(1)
            .U16(1)
            .U16(1)
            .U16(6)
            .U16(3)
            .U32(0)
            .ValueInt(7)
            .U16(0)
            .U16(1)
            .U16(1)
            .U16(1)
            .U16(4)
            .U16(5)
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(1)
            .U8((byte)PexOpCode.Nop)
            .ToArray();
    }

    private static byte[] BuildFallout4ConstAndStructs()
    {
        return new PexBlobBuilder()
            .Header(PexVersion.V39, GameIdMapping.Fallout4Raw)
            .U16(5)
            .Str("Foo")
            .Str("Bar")
            .Str("")
            .Str("Int")
            .Str("Member")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(2)
            .U16(2)
            .U8(1)
            .U32(0)
            .U16(2)
            .U16(1)
            .U16(1)
            .U16(1)
            .U16(4)
            .U16(3)
            .U32(0)
            .ValueInt(3)
            .U8(1)
            .U16(2)
            .U16(0)
            .U16(0)
            .U16(0)
            .ToArray();
    }

    private static byte[] BuildStarfieldGuards()
    {
        return new PexBlobBuilder()
            .Header(PexVersion.V312, GameIdMapping.StarfieldRaw)
            .U16(3)
            .Str("Ship")
            .Str("")
            .Str("MyGuard")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1)
            .U8(0)
            .U32(0)
            .U16(1)
            .U16(0)
            .U16(0)
            .U16(1)
            .U16(2)
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
            .U16(1)
            .U8((byte)PexOpCode.LockGuards)
            .ValueInt(0)
            .ToArray();
    }

    private static byte[] BuildPropertyWithGetter()
    {
        return new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(5)
            .Str("Script")
            .Str("")
            .Str("Prop")
            .Str("Int")
            .Str("None")
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
            .U16(1)
            .U16(2)
            .U16(3)
            .U16(1)
            .U32(0)
            .U8(0x01)
            .U16(4)
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(1)
            .U8((byte)PexOpCode.Return)
            .ValueNone()
            .U16(0)
            .ToArray();
    }

    private static byte[] BuildInstructionFile(
        PexVersion version,
        ushort rawGameId,
        bool extended,
        PexOpCode opcode,
        Action<PexBlobBuilder> args)
    {
        var b = new PexBlobBuilder()
            .Header(version, rawGameId)
            .U16(4)
            .Str("Script")
            .Str("")
            .Str("Fn")
            .Str("::dest")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1);
        if (extended)
            b.U8(0);
        b.U32(0).U16(1);
        if (extended)
            b.U16(0);
        b.U16(0);
        if (rawGameId == GameIdMapping.StarfieldRaw)
            b.U16(0);
        b.U16(0)
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
            .U16(1)
            .U8((byte)opcode);
        args(b);
        return b.ToArray();
    }
}
