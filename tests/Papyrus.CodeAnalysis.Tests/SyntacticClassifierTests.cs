using System.Collections.Immutable;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Classification;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class SyntacticClassifierTests
{
    [Fact]
    public void Classifies_keywords_literals_operators_and_declaration_names()
    {
        const string source =
            "ScriptName Foo Extends Bar\n" +
            "{Doc comment}\n" +
            "; line comment\n" +
            "Int myVar = 0x1F Conditional\n" +
            "Float Property MyProp = 1.5 Auto\n" +
            "Function DoThing(String argName = \"hi\")\n" +
            "  Int local = 1 + 2\n" +
            "  DoThing(argName = \"x\")\n" +
            "EndFunction\n" +
            "State Waiting\n" +
            "EndState\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var spans = SyntacticClassifier.Classify(tree, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ClassificationKind.Keyword, GetKind(source, spans, "ScriptName"));
        Assert.Equal(ClassificationKind.TypeName, GetKind(source, spans, "Foo"));
        Assert.True(GetSpan(source, spans, "Foo").IsDeclaration);
        Assert.Equal(ClassificationKind.TypeName, GetKind(source, spans, "Bar"));
        Assert.False(GetSpan(source, spans, "Bar").IsDeclaration);
        Assert.Equal(ClassificationKind.Comment, GetKind(source, spans, "{Doc comment}"));
        Assert.Equal(ClassificationKind.Comment, GetKind(source, spans, "; line comment"));
        Assert.Equal(ClassificationKind.VariableName, GetKind(source, spans, "myVar"));
        Assert.Equal(ClassificationKind.Number, GetKind(source, spans, "0x1F"));
        Assert.Equal(ClassificationKind.PropertyName, GetKind(source, spans, "MyProp"));
        Assert.Equal(ClassificationKind.Number, GetKind(source, spans, "1.5"));
        Assert.Equal(ClassificationKind.FunctionName, GetKind(source, spans, "DoThing"));
        Assert.True(GetSpan(source, spans, "DoThing").IsDeclaration);
        Assert.Equal(ClassificationKind.ParameterName, GetKind(source, spans, "argName"));
        Assert.Equal(ClassificationKind.String, GetKind(source, spans, "\"hi\""));
        Assert.Equal(ClassificationKind.VariableName, GetKind(source, spans, "local"));
        Assert.Equal(ClassificationKind.Operator, GetKind(source, spans, "+"));
        Assert.Equal(ClassificationKind.StateName, GetKind(source, spans, "Waiting"));

        // Named argument at the call site classifies as a parameter reference.
        var namedArgument = GetSpan(source, spans, "argName = \"x\"", "argName");
        Assert.Equal(ClassificationKind.ParameterName, namedArgument.Kind);
        Assert.False(namedArgument.IsDeclaration);

        // Spans are ordered and non-overlapping.
        for (var i = 1; i < spans.Length; i++)
        {
            Assert.True(spans[i - 1].Span.End <= spans[i].Span.Start);
        }
    }

    [Fact]
    public void Classifies_fallout4_structs_and_type_positions()
    {
        const string source =
            "ScriptName Foo\n" +
            "Struct Point\n" +
            "  Int x\n" +
            "EndStruct\n" +
            "Point Function Make(Foo:Point p)\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        var spans = SyntacticClassifier.Classify(tree, cancellationToken: TestContext.Current.CancellationToken);

        var structName = GetSpan(source, spans, "Point");
        Assert.Equal(ClassificationKind.StructName, structName.Kind);
        Assert.True(structName.IsDeclaration);
        Assert.Equal(ClassificationKind.VariableName, GetKind(source, spans, "x"));
        Assert.Equal(ClassificationKind.TypeName, GetKind(source, spans, "Point Function", "Point"));
        Assert.Equal(ClassificationKind.ParameterName, GetKind(source, spans, "p)"));
    }

    [Fact]
    public void Unresolved_expression_identifiers_are_left_unclassified()
    {
        const string source =
            "ScriptName Foo\n" +
            "Function F()\n" +
            "  someVar.someMember()\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var spans = SyntacticClassifier.Classify(tree, cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain(spans, s => Covers(source, s, "someVar"));
        Assert.DoesNotContain(spans, s => Covers(source, s, "someMember"));
    }

    [Fact]
    public void Span_filter_returns_only_intersecting_classifications()
    {
        const string source =
            "ScriptName Foo\n" +
            "Int a = 1\n" +
            "Int b = 2\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);
        var secondLine = new TextSpan(source.IndexOf("Int a"), "Int a = 1".Length);

        var spans = SyntacticClassifier.Classify(tree, secondLine, TestContext.Current.CancellationToken);

        Assert.All(spans, s =>
        {
            Assert.True(s.Span.Start < secondLine.End);
            Assert.True(s.Span.End > secondLine.Start);
        });
        Assert.Contains(spans, s => Covers(source, s, "a ="));
        Assert.DoesNotContain(spans, s => Covers(source, s, "b ="));
        Assert.DoesNotContain(spans, s => Covers(source, s, "Foo"));
    }

    private static ClassificationKind GetKind(
        string source,
        ImmutableArray<ClassifiedSpan> spans,
        string anchor,
        string? token = null)
    {
        return GetSpan(source, spans, anchor, token).Kind;
    }

    private static ClassifiedSpan GetSpan(
        string source,
        ImmutableArray<ClassifiedSpan> spans,
        string anchor,
        string? token = null)
    {
        token ??= FirstWordOf(anchor);
        var start = source.IndexOf(anchor, System.StringComparison.Ordinal);
        Assert.True(start >= 0, $"anchor not found: {anchor}");
        var match = spans.Where(s => s.Span.Start == start && s.Span.Length == token.Length).ToArray();
        Assert.True(match.Length == 1, $"expected one classification at {anchor}, found {match.Length}");
        return match[0];
    }

    private static bool Covers(string source, ClassifiedSpan span, string anchor)
    {
        var token = FirstWordOf(anchor);
        var start = source.IndexOf(anchor, System.StringComparison.Ordinal);
        return start >= 0 && span.Span.Start == start && span.Span.Length == token.Length;
    }

    private static string FirstWordOf(string anchor)
    {
        var end = 0;
        while (end < anchor.Length && (char.IsLetterOrDigit(anchor[end]) || anchor[end] is '_' or '.'))
        {
            end++;
        }

        return end == 0 ? anchor : anchor[..end];
    }
}
