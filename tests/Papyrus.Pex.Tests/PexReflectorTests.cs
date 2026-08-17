using System;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PexReflectorTests
{
    [Fact]
    public void Reflect_NullFile_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PexReflector.Reflect(null!));
    }

    [Fact]
    public void Reflect_EmptyFile_HasEmptyNameAndCollections()
    {
        PexScriptMetadata metadata = PexReflector.Reflect(new PexFile());

        Assert.Equal("", metadata.Name.Text);
        Assert.Null(metadata.Name.StructName);
        Assert.Null(metadata.BaseType);
        Assert.Empty(metadata.Functions);
        Assert.Empty(metadata.Properties);
        Assert.Empty(metadata.Structs);
        Assert.Empty(metadata.Guards);
        Assert.Empty(metadata.States);
        Assert.Equal(0u, metadata.UserFlags);
        Assert.Empty(metadata.UserFlagNames);
    }

    [Fact]
    public void Reflect_ConstructedSkyrimScript_MapsNameFunctionsAndFlags()
    {
        PexScriptMetadata metadata = PexReflector.Reflect(BuildSkyrimScript());

        Assert.Equal(new PexQualifiedName("Built"), metadata.Name);
        Assert.Null(metadata.BaseType);
        Assert.Equal(1u, metadata.UserFlags);
        Assert.Equal(32, metadata.UserFlagNames.Length);
        Assert.Equal("Hidden", metadata.UserFlagNames[0]);
        Assert.Equal("Conditional", metadata.UserFlagNames[1]);

        PexFunctionMetadata function = Assert.Single(metadata.Functions);
        Assert.Equal("Go", function.Name);
        Assert.Equal("None", function.ReturnTypeName);
        Assert.Equal(1u, function.UserFlags);
        Assert.False(function.IsNative);
        Assert.False(function.IsGlobal);

        PexParameterMetadata parameter = Assert.Single(function.Parameters);
        Assert.Equal("value", parameter.Name);
        Assert.Equal("Int", parameter.TypeName);

        Assert.Empty(metadata.Properties);
        Assert.Empty(metadata.States);
    }

    [Fact]
    public void Reflect_NamespacedNameAndBaseType_KeepsColonText()
    {
        var file = new PexFile
        {
            Objects =
            [
                new PexObject
                {
                    Name = "A:B:Script",
                    ParentClassName = "A:B:Parent",
                },
            ],
        };

        PexScriptMetadata metadata = PexReflector.Reflect(file);

        Assert.Equal("A:B:Script", metadata.Name.Text);
        Assert.Null(metadata.Name.StructName);
        Assert.Equal("A:B:Parent", metadata.BaseType?.Text);
        Assert.Null(metadata.BaseType?.StructName);
    }

    [Fact]
    public void Reflect_ScriptHashStruct_SplitsStructName()
    {
        var file = new PexFile
        {
            Objects =
            [
                new PexObject
                {
                    Name = "A:B:Script#Member",
                    ParentClassName = "Other#Inner",
                },
            ],
        };

        PexScriptMetadata metadata = PexReflector.Reflect(file);

        Assert.Equal("A:B:Script", metadata.Name.Text);
        Assert.Equal("Member", metadata.Name.StructName);
        Assert.Equal("Other", metadata.BaseType?.Text);
        Assert.Equal("Inner", metadata.BaseType?.StructName);
    }

    [Fact]
    public void Reflect_Fallout4Members_MapsPropertiesStructsAndStates()
    {
        PexScriptMetadata metadata = PexReflector.Reflect(BuildFallout4Script());

        Assert.Equal(new PexQualifiedName("Foo"), metadata.Name);
        Assert.Equal(new PexQualifiedName("Bar"), metadata.BaseType!.Value);

        PexFunctionMetadata native = Assert.Single(metadata.Functions);
        Assert.Equal("NativeFn", native.Name);
        Assert.Equal("Int", native.ReturnTypeName);
        Assert.True(native.IsNative);
        Assert.True(native.IsGlobal);

        PexPropertyMetadata property = Assert.Single(metadata.Properties);
        Assert.Equal("Prop", property.Name);
        Assert.Equal("Int", property.TypeName);
        Assert.True(property.IsAuto);
        Assert.True(property.IsReadable);
        Assert.True(property.IsWritable);
        Assert.Null(property.GetMethod);
        Assert.Null(property.SetMethod);

        PexStructMetadata struc = Assert.Single(metadata.Structs);
        Assert.Equal("Pair", struc.Name);
        PexStructMemberMetadata member = Assert.Single(struc.Members);
        Assert.Equal("Left", member.Name);
        Assert.Equal("Int", member.TypeName);
        Assert.True(member.IsConst);
        Assert.Equal(2u, member.UserFlags);

        PexGuardMetadata guard = Assert.Single(metadata.Guards);
        Assert.Equal("Busy", guard.Name);

        PexStateMetadata state = Assert.Single(metadata.States);
        Assert.Equal("BusyState", state.Name);
        Assert.True(state.IsAuto);
        PexFunctionMetadata stateFn = Assert.Single(state.Functions);
        Assert.Equal("OnBeginState", stateFn.Name);
        Assert.Equal("None", stateFn.ReturnTypeName);
    }

    [Fact]
    public void Reflect_FullProperty_MapsGetterAndSetter()
    {
        var file = new PexFile
        {
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    Properties =
                    [
                        new PexProperty
                        {
                            Name = "Value",
                            TypeName = "Int",
                            IsReadable = true,
                            IsWritable = true,
                            ReadFunction = new PexFunction
                            {
                                ReturnTypeName = "Int",
                                Instructions = [new PexInstruction { OpCode = PexOpCode.Return }],
                            },
                            WriteFunction = new PexFunction
                            {
                                ReturnTypeName = "None",
                                Parameters =
                                [
                                    new PexFunctionParameter { Name = "newValue", TypeName = "Int" },
                                ],
                            },
                        },
                    ],
                },
            ],
        };

        PexPropertyMetadata property = Assert.Single(PexReflector.Reflect(file).Properties);
        Assert.Equal("Value", property.Name);
        Assert.False(property.IsAuto);
        Assert.Equal("Int", property.GetMethod?.ReturnTypeName);
        Assert.Equal("None", property.SetMethod?.ReturnTypeName);
        Assert.Equal("newValue", Assert.Single(property.SetMethod!.Parameters).Name);
    }

    [Fact]
    public void Reflect_OutOfOrderUserFlags_IndexesNamesByBit()
    {
        var file = new PexFile
        {
            UserFlags =
            [
                new PexUserFlag { Name = "Custom", BitIndex = 5 },
                new PexUserFlag { Name = "Conditional", BitIndex = 1 },
                new PexUserFlag { Name = "Hidden", BitIndex = 0 },
            ],
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    UserFlags = 0b10_0011,
                },
            ],
        };

        PexScriptMetadata metadata = PexReflector.Reflect(file);

        Assert.Equal(0b10_0011u, metadata.UserFlags);
        Assert.Equal(32, metadata.UserFlagNames.Length);
        Assert.Equal("Hidden", metadata.UserFlagNames[0]);
        Assert.Equal("Conditional", metadata.UserFlagNames[1]);
        Assert.Equal("Custom", metadata.UserFlagNames[5]);
        Assert.Equal("", metadata.UserFlagNames[2]);
        Assert.Equal("", metadata.UserFlagNames[31]);
    }

    [Fact]
    public void Parse_PlainScript_HasNoStructName()
    {
        PexQualifiedName name = PexQualifiedName.Parse("Script");
        Assert.Equal("Script", name.Text);
        Assert.Null(name.StructName);
        Assert.Equal("Script", name.ToString());
    }

    [Fact]
    public void Parse_ScriptHashStruct_SplitsOnce()
    {
        PexQualifiedName name = PexQualifiedName.Parse("A:B:Script#Inner");
        Assert.Equal("A:B:Script", name.Text);
        Assert.Equal("Inner", name.StructName);
        Assert.Equal("A:B:Script#Inner", name.ToString());
    }

    private static PexFile BuildSkyrimScript()
    {
        return new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            UserFlags =
            [
                new PexUserFlag { Name = "Hidden", BitIndex = 0 },
                new PexUserFlag { Name = "Conditional", BitIndex = 1 },
            ],
            Objects =
            [
                new PexObject
                {
                    Name = "Built",
                    ParentClassName = "",
                    UserFlags = 1,
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
                                    Name = "Go",
                                    ReturnTypeName = "None",
                                    UserFlags = 1,
                                    Parameters =
                                    [
                                        new PexFunctionParameter { Name = "value", TypeName = "Int" },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }

    private static PexFile BuildFallout4Script()
    {
        return new PexFile
        {
            Version = PexVersion.V39,
            RawGameId = GameIdMapping.Fallout4Raw,
            Game = GameId.Fallout4,
            Objects =
            [
                new PexObject
                {
                    Name = "Foo",
                    ParentClassName = "Bar",
                    AutoStateName = "BusyState",
                    Structs =
                    [
                        new PexStruct
                        {
                            Name = "Pair",
                            Members =
                            [
                                new PexStructMember
                                {
                                    Name = "Left",
                                    TypeName = "Int",
                                    UserFlags = 2,
                                    IsConst = true,
                                },
                            ],
                        },
                    ],
                    Guards =
                    [
                        new PexGuard { Name = "Busy" },
                    ],
                    Properties =
                    [
                        new PexProperty
                        {
                            Name = "Prop",
                            TypeName = "Int",
                            IsAuto = true,
                            IsReadable = true,
                            IsWritable = true,
                            AutoVar = "::Prop",
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
                                    Name = "NativeFn",
                                    ReturnTypeName = "Int",
                                    IsNative = true,
                                    IsGlobal = true,
                                },
                            ],
                        },
                        new PexState
                        {
                            Name = "BusyState",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "OnBeginState",
                                    ReturnTypeName = "None",
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
