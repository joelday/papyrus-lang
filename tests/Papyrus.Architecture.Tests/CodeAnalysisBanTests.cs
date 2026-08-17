using System;
using System.Collections.Generic;
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Papyrus.Architecture.Tests;

public sealed class CodeAnalysisBanTests
{
    private static readonly Assembly CodeAnalysis = Assembly.Load("Papyrus.CodeAnalysis");

    [Fact]
    public void Must_not_use_file_io_apis()
    {
        var result = Types.InAssembly(CodeAnalysis)
            .ShouldNot()
            .HaveDependencyOnAny(
                "System.IO.File",
                "System.IO.Directory",
                "System.IO.FileStream",
                "System.IO.FileSystemWatcher",
                "System.IO.FileInfo",
                "System.IO.DirectoryInfo",
                "System.IO.StreamReader",
                "System.IO.StreamWriter")
            .GetResult();
        AssertSuccessful(result, "Papyrus.CodeAnalysis references a banned System.IO file API.");

        using var definition = CodeAnalysisInspector.ReadCodeAnalysis();
        AssertNoHits(CodeAnalysisInspector.FindFileIoUsages(definition));
    }

    [Fact]
    public void Must_not_use_system_reflection()
    {
        // SDK-generated Assembly* attributes are not a reflection use.
        // There is no System.Reflection.Metadata carve-out.
        using var definition = CodeAnalysisInspector.ReadCodeAnalysis();
        AssertNoHits(CodeAnalysisInspector.FindReflectionUsages(definition));
    }

    [Fact]
    public void Must_not_use_runtime_loader()
    {
        var result = Types.InAssembly(CodeAnalysis)
            .ShouldNot()
            .HaveDependencyOn("System.Runtime.Loader")
            .GetResult();
        AssertSuccessful(result, "Papyrus.CodeAnalysis references System.Runtime.Loader.");

        using var definition = CodeAnalysisInspector.ReadCodeAnalysis();
        AssertNoHits(CodeAnalysisInspector.FindRuntimeLoaderUsages(definition));
    }

    [Fact]
    public void Must_not_mark_apis_requires_dynamic_or_unreferenced_code()
    {
        using var definition = CodeAnalysisInspector.ReadCodeAnalysis();
        AssertNoHits(CodeAnalysisInspector.FindRequiresDynamicOrUnreferencedCode(definition));
    }

    [Fact]
    public void JsonSerializer_must_use_source_generated_context()
    {
        using var definition = CodeAnalysisInspector.ReadCodeAnalysis();
        AssertNoHits(CodeAnalysisInspector.FindReflectionFreeJsonSerializerCalls(definition));
    }

    [Fact]
    public void Must_not_read_environment_variables()
    {
        using var definition = CodeAnalysisInspector.ReadCodeAnalysis();
        AssertNoHits(
            CodeAnalysisInspector.FindMethodCalls(definition, "System.Environment", "GetEnvironmentVariable"));
    }

    [Fact]
    public void Must_not_call_encoding_get_encoding()
    {
        using var definition = CodeAnalysisInspector.ReadCodeAnalysis();
        AssertNoHits(
            CodeAnalysisInspector.FindMethodCalls(definition, "System.Text.Encoding", "GetEncoding"));
    }

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result, string headline)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var failing = result.FailingTypeNames ?? Array.Empty<string>();
        Assert.Fail(headline + Environment.NewLine + string.Join(Environment.NewLine, failing));
    }

    private static void AssertNoHits(IReadOnlyList<string> hits)
    {
        if (hits.Count == 0)
        {
            return;
        }

        Assert.Fail(string.Join(Environment.NewLine, hits));
    }
}
