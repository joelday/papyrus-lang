using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Classification;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class SemanticClassifierTests
{
    private const string Other =
        "ScriptName Other\nStruct Point\n  Int X\nEndStruct\nInt Function Twice(Int a) Global\n  return a * 2\nEndFunction\n"
        + "Function Ping()\nEndFunction\nEvent OnPing()\nEndEvent\n";

    private const string Source =
        "ScriptName Foo\n"
        + "Other Property Target Auto\n"
        + "Int Property Limit = 5 AutoReadOnly\n"
        + "Int Max = 3 Const\n"
        + "Other:Point[] points\n"
        + "Function Go(Int count)\n"
        + "{Does things}\n"
        + "  Int total = Other.Twice(count) + Limit + Max\n"
        + "  Target.Ping()\n"
        + "  points[total].X = 1\n"
        + "  Self.Go(total)\n"
        + "EndFunction\n";

    [Fact]
    public void References_take_the_kind_of_what_they_resolve_to()
    {
        var spans = Classify();

        Assert.Equal(ClassificationKind.TypeName, At(spans, "Other.Twice", "Other").Kind);
        Assert.Equal(ClassificationKind.FunctionName, At(spans, "Twice(count)", "Twice").Kind);
        Assert.Equal(ClassificationKind.ParameterName, At(spans, "count) +", "count").Kind);
        Assert.Equal(ClassificationKind.PropertyName, At(spans, "Limit + Max", "Limit").Kind);
        Assert.Equal(ClassificationKind.VariableName, At(spans, "Max\n", "Max").Kind);
        Assert.Equal(ClassificationKind.PropertyName, At(spans, "Target.Ping", "Target").Kind);
        Assert.Equal(ClassificationKind.FunctionName, At(spans, "Ping()\n  points", "Ping").Kind);
        Assert.Equal(ClassificationKind.VariableName, At(spans, "points[total]", "points").Kind);
        Assert.Equal(ClassificationKind.VariableName, At(spans, "total].X", "total").Kind);
        Assert.Equal(ClassificationKind.VariableName, At(spans, "X = 1", "X").Kind);
        Assert.Equal(ClassificationKind.Keyword, At(spans, "Self.Go", "Self").Kind);
        Assert.Equal(ClassificationKind.FunctionName, At(spans, "Go(total)", "Go").Kind);
    }

    [Fact]
    public void Struct_type_names_are_structs()
    {
        var spans = Classify();

        Assert.Equal(ClassificationKind.TypeName, At(spans, "Other:Point[]", "Other").Kind);
        Assert.Equal(ClassificationKind.StructName, At(spans, "Point[]", "Point").Kind);
    }

    [Fact]
    public void Modifiers_mark_global_readonly_declaration_and_documentation()
    {
        var spans = Classify();

        Assert.True(At(spans, "Twice(count)", "Twice").Modifiers.HasFlag(ClassificationModifiers.Static));
        Assert.True(At(spans, "Limit + Max", "Limit").Modifiers.HasFlag(ClassificationModifiers.ReadOnly));
        Assert.True(At(spans, "Max = 3 Const", "Max").Modifiers.HasFlag(ClassificationModifiers.ReadOnly));
        Assert.Equal(
            ClassificationModifiers.Declaration | ClassificationModifiers.ReadOnly,
            At(spans, "Max = 3 Const", "Max").Modifiers);
        Assert.Equal(ClassificationModifiers.None, At(spans, "Target.Ping", "Target").Modifiers);
        Assert.Equal(ClassificationModifiers.Documentation, At(spans, "{Does things}", "{Does things}").Modifiers);
    }

    private static ImmutableArray<ClassifiedSpan> Classify()
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse("Foo")] = SourceText.From(Source),
            [QualifiedName.Parse("Other")] = SourceText.From(Other),
        };
        var compilation = PapyrusCompilation.Create(PapyrusDialect.Fallout4, new TestScriptResolver(sources));
        var model = compilation.GetSemanticModel(compilation.GetSyntaxTree(QualifiedName.Parse("Foo")));
        return SemanticClassifier.Classify(model, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static ClassifiedSpan At(ImmutableArray<ClassifiedSpan> spans, string anchor, string token)
    {
        var start = Source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, $"anchor not found: {anchor}");
        start += anchor.IndexOf(token, StringComparison.Ordinal);
        return Assert.Single(spans, span => span.Span.Start == start && span.Span.Length == token.Length);
    }
}
