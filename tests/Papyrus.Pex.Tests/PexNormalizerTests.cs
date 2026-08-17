using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PexNormalizerTests
{
    [Fact]
    public void Hash_FilesDifferingOnlyInPathTimeUser_AreEqual()
    {
        PexFile a = BuildScript(
            sourcePath: @"C:\Work\Alice.psc",
            compileTime: 1_700_000_000,
            userName: "alice",
            machineName: "ALICE-PC",
            stringTable: ["Script", "", "Fn", "None"]);
        PexFile b = BuildScript(
            sourcePath: @"D:\Other\Bob.psc",
            compileTime: 1_800_000_111,
            userName: "bob",
            machineName: "BOB-LAPTOP",
            stringTable: ["Script", "", "Fn", "None"]);

        Assert.Equal(PexNormalizer.Hash(a), PexNormalizer.Hash(b));
        Assert.Equal(PexWriter.Write(PexNormalizer.Normalize(a)), PexWriter.Write(PexNormalizer.Normalize(b)));
    }

    [Fact]
    public void Hash_FilesDifferingOnlyInStringTableOrder_AreEqual()
    {
        PexFile firstUse = BuildScript(stringTable: ["Script", "", "Fn", "None"]);
        PexFile shuffled = BuildScript(stringTable: ["None", "Unused", "Fn", "Script", ""]);

        Assert.Equal(PexNormalizer.Hash(firstUse), PexNormalizer.Hash(shuffled));
    }

    [Fact]
    public void Hash_IsStableLowercaseSha256Hex()
    {
        PexFile file = BuildScript();
        string hash = PexNormalizer.Hash(file);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, PexNormalizer.Hash(file));
        Assert.Equal(hash, PexNormalizer.Hash(PexNormalizer.Normalize(file)));

        byte[] expected = SHA256.HashData(PexWriter.Write(PexNormalizer.Normalize(file)));
        Assert.Equal(Convert.ToHexStringLower(expected), hash);
    }

    [Fact]
    public void Normalize_ClearsIncidentalHeaderFields()
    {
        PexFile file = BuildScript(
            sourcePath: "Built.psc",
            compileTime: 99,
            userName: "builder",
            machineName: "box");

        PexFile normalized = PexNormalizer.Normalize(file);

        Assert.Equal("", normalized.SourcePath);
        Assert.Equal(DateTimeOffset.UnixEpoch, normalized.CompileTime);
        Assert.Equal("", normalized.UserName);
        Assert.Equal("", normalized.MachineName);
        Assert.Equal(file.Version, normalized.Version);
        Assert.Equal(file.RawGameId, normalized.RawGameId);
        Assert.Equal(file.Game, normalized.Game);
        Assert.Equal(file.Objects[0].Name, normalized.Objects[0].Name);
    }

    [Fact]
    public void Normalize_DoesNotMutateInput()
    {
        PexFile file = BuildScript(
            sourcePath: "Built.psc",
            compileTime: 99,
            userName: "builder",
            machineName: "box",
            stringTable: ["Unused", "None", "Fn", "Script", ""]);

        ImmutableArray<string> originalTable = file.StringTable;
        _ = PexNormalizer.Normalize(file);

        Assert.Equal("Built.psc", file.SourcePath);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(99), file.CompileTime);
        Assert.Equal("builder", file.UserName);
        Assert.Equal("box", file.MachineName);
        Assert.Equal(originalTable, file.StringTable);
    }

    [Fact]
    public void Normalize_RebuildsStringTableInFirstUseOrder()
    {
        PexFile file = BuildScript(stringTable: ["Unused", "None", "Fn", "Script", ""]);

        PexFile normalized = PexNormalizer.Normalize(file);

        Assert.Equal(new[] { "Script", "", "Fn", "None" }, normalized.StringTable);
    }

    [Fact]
    public void Normalize_PreservesDebugInfo()
    {
        PexFile file = BuildScript();
        PexFile normalized = PexNormalizer.Normalize(file);

        PexDebugInfo debug = Assert.IsType<PexDebugInfo>(normalized.DebugInfo);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(7), debug.ModificationTime);
        Assert.Equal("Script", debug.Functions[0].ObjectName);
        Assert.Equal("Fn", debug.Functions[0].FunctionName);
        Assert.Equal(new ushort[] { 12 }, debug.Functions[0].InstructionLineMap);
    }

    [Fact]
    public void Normalize_AndHash_NullFile_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PexNormalizer.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => PexNormalizer.Hash(null!));
    }

    [Fact]
    public void Hash_BlobFixturesDifferingOnlyInPathTimeUser_AreEqual()
    {
        byte[] a = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw, compileTime: 100, source: "A.psc", user: "alice", machine: "pc1")
            .EmptyTail()
            .ToArray();
        byte[] b = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw, compileTime: 200, source: "B.psc", user: "bob", machine: "pc2")
            .EmptyTail()
            .ToArray();

        Assert.NotEqual(a, b);
        Assert.Equal(PexNormalizer.Hash(PexReader.Read(a)), PexNormalizer.Hash(PexReader.Read(b)));
    }

    private static PexFile BuildScript(
        string sourcePath = "Built.psc",
        long compileTime = 99,
        string userName = "builder",
        string machineName = "box",
        string[]? stringTable = null)
    {
        return new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            CompileTime = DateTimeOffset.FromUnixTimeSeconds(compileTime),
            SourcePath = sourcePath,
            UserName = userName,
            MachineName = machineName,
            StringTable = stringTable is null ? ["Script", "", "Fn", "None"] : [.. stringTable],
            DebugInfo = new PexDebugInfo
            {
                ModificationTime = DateTimeOffset.FromUnixTimeSeconds(7),
                Functions =
                [
                    new PexDebugFunction
                    {
                        ObjectName = "Script",
                        StateName = "",
                        FunctionName = "Fn",
                        FunctionType = PexDebugFunctionType.Normal,
                        InstructionLineMap = [12],
                    },
                ],
            },
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    ParentClassName = "",
                    DocumentationString = "",
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
                                    DocumentationString = "",
                                    Instructions =
                                    [
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
