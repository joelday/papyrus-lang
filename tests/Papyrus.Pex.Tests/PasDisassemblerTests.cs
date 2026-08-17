using System;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PasDisassemblerTests
{
    [Fact]
    public void Disassemble_SyntheticFile_ContainsObjectFunctionAndOpcodes()
    {
        string pas = PasDisassembler.Disassemble(BuildSyntheticSkyrimFile());

        Assert.Contains(".object Built ", pas);
        Assert.Contains(".function Go", pas);
        Assert.Contains("IADD ::tmp 1 2", pas);
        Assert.Contains("RETURN None", pas);
        Assert.Contains(PasDisassembler.OpCodeName(PexOpCode.IAdd), pas);
        Assert.Contains(PasDisassembler.OpCodeName(PexOpCode.Return), pas);
    }

    [Fact]
    public void Disassemble_EmitsHeaderUserFlagsAndTables()
    {
        string pas = PasDisassembler.Disassemble(BuildSyntheticSkyrimFile());

        Assert.Contains(".info", pas);
        Assert.Contains(".source \"Built.psc\"", pas);
        Assert.Contains(".user \"builder\"", pas);
        Assert.Contains(".computer \"box\"", pas);
        Assert.Contains(".compileTime 99", pas);
        Assert.Contains(".userFlagsRef", pas);
        Assert.Contains(".flag Hidden 0", pas);
        Assert.Contains(".endUserFlagsRef", pas);
        Assert.Contains(".objectTable", pas);
        Assert.Contains(".endObjectTable", pas);
        Assert.Contains(".state", pas);
        Assert.Contains(".code", pas);
        Assert.Contains(".endCode", pas);
        Assert.Contains(".endFunction", pas);
        Assert.Contains("IADD ::tmp 1 2 ;@line 12", pas);
    }

    [Fact]
    public void Disassemble_Twice_IsDeterministic()
    {
        PexFile file = BuildSyntheticSkyrimFile();
        Assert.Equal(PasDisassembler.Disassemble(file), PasDisassembler.Disassemble(file));
    }

    [Fact]
    public void Disassemble_EmptyFile_EmitsSkeleton()
    {
        string pas = PasDisassembler.Disassemble(new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
        });

        Assert.Equal(
            """
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
            .endObjectTable

            """.ReplaceLineEndings("\n"),
            pas);
    }

    [Fact]
    public void Disassemble_Jumps_EmitsStableLabels()
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

        string pas = PasDisassembler.Disassemble(file);

        Assert.Contains(".object Jumps ", pas);
        Assert.Contains(".function Loop", pas);
        Assert.Contains("label0:", pas);
        Assert.Contains("label1:", pas);
        Assert.Contains("JUMPT cond label0", pas);
        Assert.Contains("JUMP label1", pas);
        Assert.Contains("RETURN None", pas);
        Assert.DoesNotContain("JUMP -1", pas);
    }

    [Fact]
    public void Disassemble_CallMethod_EmitsVariadicArguments()
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

        string pas = PasDisassembler.Disassemble(file);
        Assert.Contains("CALLMETHOD \"Foo\" self ::dest 1 ::tmp", pas);
    }

    [Fact]
    public void Disassemble_PropertyAccessor_UsesGetName()
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

        string pas = PasDisassembler.Disassemble(file);
        Assert.Contains(".property Prop Int", pas);
        Assert.Contains(".function get", pas);
        Assert.Contains("RETURN 3", pas);
        Assert.DoesNotContain(".function set", pas);
    }

    [Fact]
    public void Disassemble_Fallout4ConstAndStruct_EmitsModifiers()
    {
        var file = new PexFile
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

        string pas = PasDisassembler.Disassemble(file);
        Assert.Contains(".object Foo Bar const", pas);
        Assert.Contains(".struct Pair", pas);
        Assert.Contains(".variable Member Int const", pas);
        Assert.Contains(".initialValue 3", pas);
        Assert.Contains(".variable ::auto Int const", pas);
        Assert.Contains(".initialValue True", pas);
    }

    [Fact]
    public void Disassemble_EscapesQuotedStrings()
    {
        var file = new PexFile
        {
            Version = PexVersion.V32,
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

        string pas = PasDisassembler.Disassemble(file);
        Assert.Contains(".source \"path\\\\with\\\"quotes\\n\"", pas);
        Assert.Contains(".user \"a\\tb\"", pas);
        Assert.Contains(".docString \"line1\\nline2\"", pas);
        Assert.Contains("ASSIGN ::s \"say \\\"hi\\\"\"", pas);
    }

    [Fact]
    public void Disassemble_NullFile_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PasDisassembler.Disassemble(null!));
    }

    [Theory]
    [InlineData(PexOpCode.Nop, "NOOP")]
    [InlineData(PexOpCode.IAdd, "IADD")]
    [InlineData(PexOpCode.ISub, "ISUBTRACT")]
    [InlineData(PexOpCode.Jmp, "JUMP")]
    [InlineData(PexOpCode.CmpEq, "COMPAREEQ")]
    [InlineData(PexOpCode.CallMethod, "CALLMETHOD")]
    [InlineData(PexOpCode.Return, "RETURN")]
    [InlineData(PexOpCode.ArrayClear, "ARRAYCLEARELEMENTS")]
    [InlineData(PexOpCode.LockGuards, "LOCKGUARDS")]
    [InlineData(PexOpCode.Invalid, "INVALID")]
    public void OpCodeName_MatchesCapricaPas(PexOpCode op, string expected)
    {
        Assert.Equal(expected, PasDisassembler.OpCodeName(op));
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
}
