using System;
using System.Linq;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PasAssemblerTests
{
    [Fact]
    public void Assemble_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PasAssembler.Assemble(null!));
    }

    [Fact]
    public void Assemble_Disassemble_EmptyFile_PasIsIdempotent()
    {
        PexFile original = new()
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
        };

        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_Synthetic_StructurallyEquals()
    {
        PexFile original = BuildSyntheticSkyrimFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_Jumps_ResolvesRelativeOffsets()
    {
        PexFile original = BuildJumpsFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        PexInstruction[] instructions = assembled.Objects[0].States[0].Functions[0].Instructions.ToArray();
        Assert.Equal(PexOpCode.JmpT, instructions[0].OpCode);
        Assert.Equal(PexValueType.Identifier, instructions[0].Arguments[0].Type);
        Assert.Equal("cond", instructions[0].Arguments[0].Text);
        Assert.Equal(2, instructions[0].Arguments[1].Integer);
        Assert.Equal(PexOpCode.Jmp, instructions[1].OpCode);
        Assert.Equal(-1, instructions[1].Arguments[0].Integer);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_CallMethod_SplitsVariadicArguments()
    {
        PexFile original = BuildCallMethodFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        PexInstruction call = assembled.Objects[0].States[0].Functions[0].Instructions[0];
        Assert.Equal(PexOpCode.CallMethod, call.OpCode);
        Assert.Equal(3, call.Arguments.Length);
        Assert.Equal(PexValueType.String, call.Arguments[0].Type);
        Assert.Equal("Foo", call.Arguments[0].Text);
        Assert.Equal(2, call.VariadicArguments.Length);
        Assert.Equal(1, call.VariadicArguments[0].Integer);
        Assert.Equal("::tmp", call.VariadicArguments[1].Text);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_PropertyGetter_RoundTrips()
    {
        PexFile original = BuildPropertyGetterFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        PexProperty property = assembled.Objects[0].Properties[0];
        Assert.Equal("Prop", property.Name);
        Assert.True(property.IsReadable);
        Assert.False(property.IsWritable);
        Assert.NotNull(property.ReadFunction);
        Assert.Equal("", property.ReadFunction!.Name);
        Assert.Equal(PexOpCode.Return, property.ReadFunction.Instructions[0].OpCode);
        Assert.Equal(3, property.ReadFunction.Instructions[0].Arguments[0].Integer);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_Fallout4ConstAndStruct_InfersGame()
    {
        PexFile original = BuildFallout4File();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        Assert.Equal(GameId.Fallout4, assembled.Game);
        Assert.Equal(PexVersion.V39, assembled.Version);
        Assert.Equal(GameIdMapping.Fallout4Raw, assembled.RawGameId);
        Assert.True(assembled.Objects[0].IsConst);
        Assert.Equal("Pair", assembled.Objects[0].Structs[0].Name);
        Assert.True(assembled.Objects[0].Structs[0].Members[0].IsConst);
        Assert.Equal(3, assembled.Objects[0].Structs[0].Members[0].DefaultValue.Integer);
        Assert.True(assembled.Objects[0].Variables[0].IsConst);
        Assert.True(assembled.Objects[0].Variables[0].DefaultValue.Bool);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_EscapesQuotedStrings()
    {
        PexFile original = BuildEscapedFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        Assert.Equal("path\\with\"quotes\n", assembled.SourcePath);
        Assert.Equal("a\tb", assembled.UserName);
        Assert.Equal("line1\nline2", assembled.Objects[0].DocumentationString);
        Assert.Equal("say \"hi\"", assembled.Objects[0].States[0].Functions[0].Instructions[0].Arguments[1].Text);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_AutoPropertyAndGroup_RoundTrips()
    {
        PexFile original = BuildAutoPropertyFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        PexProperty property = assembled.Objects[0].Properties[0];
        Assert.True(property.IsAuto);
        Assert.True(property.IsReadable);
        Assert.True(property.IsWritable);
        Assert.Equal("::auto", property.AutoVar);
        Assert.NotNull(assembled.DebugInfo);
        Assert.Single(assembled.DebugInfo!.PropertyGroups);
        Assert.Equal("Bar", assembled.DebugInfo.PropertyGroups[0].GroupName);
        Assert.Equal("Prop", assembled.DebugInfo.PropertyGroups[0].Properties[0]);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_StarfieldGuards_InfersGame()
    {
        PexFile original = BuildStarfieldGuardsFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        Assert.Equal(GameId.Starfield, assembled.Game);
        Assert.Equal(PexVersion.V312, assembled.Version);
        Assert.Equal("Mutex", assembled.Objects[0].Guards[0].Name);
        Assert.Equal(PexOpCode.LockGuards, assembled.Objects[0].States[0].Functions[0].Instructions[0].OpCode);
        Assert.Empty(assembled.Objects[0].States[0].Functions[0].Instructions[0].Arguments);
        Assert.Equal("Mutex", assembled.Objects[0].States[0].Functions[0].Instructions[0].VariadicArguments[0].Text);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_Disassemble_NativeStatic_RoundTrips()
    {
        PexFile original = BuildNativeStaticFile();
        PexFile assembled = PasAssembler.Assemble(PasDisassembler.Disassemble(original));

        PexFunction function = assembled.Objects[0].States[0].Functions[0];
        Assert.True(function.IsNative);
        Assert.True(function.IsGlobal);
        Assert.Empty(function.Locals);
        Assert.Empty(function.Instructions);
        Assert.Equal("Int", function.ReturnTypeName);
        Assert.Equal("n", function.Parameters[0].Name);

        AssertPasVisibleEqual(original, assembled);
        Assert.Equal(PexWriter.Write(original), PexWriter.Write(assembled));
        AssertPasIdempotent(original);
    }

    [Fact]
    public void Assemble_UnknownOpcode_Throws()
    {
        const string pas = """
            .info
              .source ""
              .modifyTime 0 ;Debug info: No
              .compileTime 0
              .user ""
              .computer ""
            .endInfo
            .userFlagsRef
            .endUserFlagsRef
            .objectTable
              .object Script
                .userFlags 0
                .docString ""
                .autoState
                .structTable
                .endStructTable
                .variableTable
                .endVariableTable
                .guardTable
                .endGuardTable
                .propertyTable
                .endPropertyTable
                .propertyGroupTable
                .endPropertyGroupTable
                .stateTable
                  .state
                    .function Go
                      .userFlags 0
                      .docString ""
                      .return None
                      .paramTable
                      .endParamTable
                      .localTable
                      .endLocalTable
                      .code
                        NOTANOPCODE
                      .endCode
                    .endFunction
                  .endState
                .endStateTable
              .endObject
            .endObjectTable
            """;

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PasAssembler.Assemble(pas));
        Assert.Contains("unknown opcode", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_MissingInfo_Throws()
    {
        Assert.Throws<PexFormatException>(() => PasAssembler.Assemble(".objectTable\n.endObjectTable\n"));
    }

    private static void AssertPasIdempotent(PexFile file)
    {
        string pas = PasDisassembler.Disassemble(file);
        string again = PasDisassembler.Disassemble(PasAssembler.Assemble(pas));
        Assert.Equal(pas, again);
    }

    private static void AssertPasVisibleEqual(PexFile expected, PexFile actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.RawGameId, actual.RawGameId);
        Assert.Equal(expected.Game, actual.Game);
        Assert.Equal(ToUnix(expected.CompileTime), ToUnix(actual.CompileTime));
        Assert.Equal(expected.SourcePath, actual.SourcePath);
        Assert.Equal(expected.UserName, actual.UserName);
        Assert.Equal(expected.MachineName, actual.MachineName);

        Assert.Equal(expected.DebugInfo is null, actual.DebugInfo is null);
        if (expected.DebugInfo is not null && actual.DebugInfo is not null)
        {
            Assert.Equal(ToUnix(expected.DebugInfo.ModificationTime), ToUnix(actual.DebugInfo.ModificationTime));
            Assert.Equal(expected.DebugInfo.Functions.Length, actual.DebugInfo.Functions.Length);
            for (int i = 0; i < expected.DebugInfo.Functions.Length; i++)
            {
                PexDebugFunction e = expected.DebugInfo.Functions[i];
                PexDebugFunction a = actual.DebugInfo.Functions[i];
                Assert.Equal(e.ObjectName, a.ObjectName);
                Assert.Equal(e.StateName, a.StateName);
                Assert.Equal(e.FunctionName, a.FunctionName);
                Assert.Equal(e.FunctionType, a.FunctionType);
                Assert.Equal(e.InstructionLineMap.ToArray(), a.InstructionLineMap.ToArray());
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
                Assert.Equal(e.Properties.ToArray(), a.Properties.ToArray());
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

    private static void AssertValueEqual(PexValue expected, PexValue actual)
    {
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Equal(expected.Integer, actual.Integer);
        Assert.Equal(expected.Float, actual.Float);
        Assert.Equal(expected.Bool, actual.Bool);
    }

    private static ulong ToUnix(DateTimeOffset time)
    {
        long seconds = time.ToUnixTimeSeconds();
        return seconds < 0 ? 0UL : (ulong)seconds;
    }

    private static PexFile BuildSyntheticSkyrimFile()
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
                                        new PexFunctionParameter { Name = "n", TypeName = "Int" },
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

    private static PexFile BuildJumpsFile()
    {
        return new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            Objects =
            [
                new PexObject
                {
                    Name = "Jumps",
                    ParentClassName = "",
                    AutoStateName = "",
                    States =
                    [
                        new PexState
                        {
                            Name = "",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "Loop",
                                    ReturnTypeName = "None",
                                    Instructions =
                                    [
                                        new PexInstruction
                                        {
                                            OpCode = PexOpCode.JmpT,
                                            Arguments =
                                            [
                                                new PexValue { Type = PexValueType.Identifier, Text = "cond" },
                                                new PexValue { Type = PexValueType.Integer, Integer = 2 },
                                            ],
                                        },
                                        new PexInstruction
                                        {
                                            OpCode = PexOpCode.Jmp,
                                            Arguments =
                                            [
                                                new PexValue { Type = PexValueType.Integer, Integer = -1 },
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

    private static PexFile BuildCallMethodFile()
    {
        return new PexFile
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
                    AutoStateName = "",
                    States =
                    [
                        new PexState
                        {
                            Name = "",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "Fn",
                                    ReturnTypeName = "None",
                                    Instructions =
                                    [
                                        new PexInstruction
                                        {
                                            OpCode = PexOpCode.CallMethod,
                                            Arguments =
                                            [
                                                new PexValue { Type = PexValueType.String, Text = "Foo" },
                                                new PexValue { Type = PexValueType.Identifier, Text = "self" },
                                                new PexValue { Type = PexValueType.Identifier, Text = "::dest" },
                                            ],
                                            VariadicArguments =
                                            [
                                                new PexValue { Type = PexValueType.Integer, Integer = 1 },
                                                new PexValue { Type = PexValueType.Identifier, Text = "::tmp" },
                                            ],
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

    private static PexFile BuildPropertyGetterFile()
    {
        return new PexFile
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
                    AutoStateName = "",
                    Properties =
                    [
                        new PexProperty
                        {
                            Name = "Prop",
                            TypeName = "Int",
                            DocumentationString = "Docs",
                            IsReadable = true,
                            ReadFunction = new PexFunction
                            {
                                ReturnTypeName = "Int",
                                Instructions =
                                [
                                    new PexInstruction
                                    {
                                        OpCode = PexOpCode.Return,
                                        Arguments =
                                        [
                                            new PexValue { Type = PexValueType.Integer, Integer = 3 },
                                        ],
                                    },
                                ],
                            },
                        },
                    ],
                },
            ],
        };
    }

    private static PexFile BuildFallout4File()
    {
        return new PexFile
        {
            Version = PexVersion.V39,
            RawGameId = GameIdMapping.Fallout4Raw,
            Game = GameId.Fallout4,
            SourcePath = "Foo.psc",
            Objects =
            [
                new PexObject
                {
                    Name = "Foo",
                    ParentClassName = "Bar",
                    IsConst = true,
                    AutoStateName = "",
                    Structs =
                    [
                        new PexStruct
                        {
                            Name = "Pair",
                            Members =
                            [
                                new PexStructMember
                                {
                                    Name = "Member",
                                    TypeName = "Int",
                                    IsConst = true,
                                    DefaultValue = new PexValue { Type = PexValueType.Integer, Integer = 3 },
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
                },
            ],
        };
    }

    private static PexFile BuildEscapedFile()
    {
        return new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            SourcePath = "path\\with\"quotes\n",
            UserName = "a\tb",
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    ParentClassName = "",
                    DocumentationString = "line1\nline2",
                    AutoStateName = "",
                    States =
                    [
                        new PexState
                        {
                            Name = "",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "Fn",
                                    ReturnTypeName = "String",
                                    Instructions =
                                    [
                                        new PexInstruction
                                        {
                                            OpCode = PexOpCode.Assign,
                                            Arguments =
                                            [
                                                new PexValue { Type = PexValueType.Identifier, Text = "::s" },
                                                new PexValue { Type = PexValueType.String, Text = "say \"hi\"" },
                                            ],
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

    private static PexFile BuildAutoPropertyFile()
    {
        return new PexFile
        {
            Version = PexVersion.V39,
            RawGameId = GameIdMapping.Fallout4Raw,
            Game = GameId.Fallout4,
            SourcePath = "Foo.psc",
            UserName = "u",
            MachineName = "h",
            DebugInfo = new PexDebugInfo
            {
                ModificationTime = DateTimeOffset.FromUnixTimeSeconds(1),
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
            },
            Objects =
            [
                new PexObject
                {
                    Name = "Foo",
                    ParentClassName = "",
                    IsConst = true,
                    AutoStateName = "",
                    Variables =
                    [
                        new PexVariable
                        {
                            Name = "::auto",
                            TypeName = "Int",
                            IsConst = true,
                            DefaultValue = new PexValue { Type = PexValueType.Integer, Integer = 0 },
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

    private static PexFile BuildStarfieldGuardsFile()
    {
        return new PexFile
        {
            Version = PexVersion.V312,
            RawGameId = GameIdMapping.StarfieldRaw,
            Game = GameId.Starfield,
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    ParentClassName = "",
                    AutoStateName = "",
                    Guards =
                    [
                        new PexGuard { Name = "Mutex" },
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
                                    Name = "Lock",
                                    ReturnTypeName = "None",
                                    Instructions =
                                    [
                                        new PexInstruction
                                        {
                                            OpCode = PexOpCode.LockGuards,
                                            VariadicArguments =
                                            [
                                                new PexValue { Type = PexValueType.Identifier, Text = "Mutex" },
                                            ],
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

    private static PexFile BuildNativeStaticFile()
    {
        return new PexFile
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
                    AutoStateName = "",
                    States =
                    [
                        new PexState
                        {
                            Name = "",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "NativeFn",
                                    ReturnTypeName = "Int",
                                    IsNative = true,
                                    IsGlobal = true,
                                    Parameters =
                                    [
                                        new PexFunctionParameter { Name = "n", TypeName = "Int" },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
