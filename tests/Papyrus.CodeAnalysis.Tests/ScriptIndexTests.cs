using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Structure;
using Papyrus.CodeAnalysis.Syntax;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class ScriptIndexTests
{
    private const string Source =
        "ScriptName A:Foo Extends Bar\n"
        + "Int Property Count Auto\n"
        + "Group Settings\n  Float Property Speed Auto\nEndGroup\n"
        + "Struct Point\n  Int X\nEndStruct\n"
        + "Int total\n"
        + "CustomEvent OnThing\n"
        + "Function Go()\n  Other.Ping()\nEndFunction\n"
        + "State Busy\n  Event OnActivate(ObjectReference akRef)\n  EndEvent\nEndState\n";

    [Fact]
    public void Declarations_are_indexed_with_their_containers()
    {
        var index = ScriptIndex.Get(SyntaxTree.Parse(Source, PapyrusDialect.Fallout4));

        Assert.Equal(
            [
                ("A:Foo", IndexedDeclarationKind.Script, null),
                ("Count", IndexedDeclarationKind.Property, "A:Foo"),
                ("Settings", IndexedDeclarationKind.Group, "A:Foo"),
                ("Speed", IndexedDeclarationKind.Property, "A:Foo"),
                ("Point", IndexedDeclarationKind.Struct, "A:Foo"),
                ("X", IndexedDeclarationKind.StructMember, "A:Foo.Point"),
                ("total", IndexedDeclarationKind.Variable, "A:Foo"),
                ("OnThing", IndexedDeclarationKind.CustomEvent, "A:Foo"),
                ("Go", IndexedDeclarationKind.Function, "A:Foo"),
                ("Busy", IndexedDeclarationKind.State, "A:Foo"),
                ("OnActivate", IndexedDeclarationKind.Event, "A:Foo.Busy"),
            ],
            index.Declarations.Select(static d => (d.Name, d.Kind, d.Container)));
        var go = index.Declarations.Single(static d => d.Name == "Go");
        Assert.Equal("Go", Source.Substring(go.NameSpan.Start, go.NameSpan.Length));
    }

    [Fact]
    public void Mentions_are_case_insensitive_identifiers_only()
    {
        var index = ScriptIndex.Get(SyntaxTree.Parse(Source, PapyrusDialect.Fallout4));

        Assert.True(index.MentionsName("ping"));
        Assert.True(index.MentionsName("BAR"));
        Assert.False(index.MentionsName("Function")); // a keyword, not an identifier
        Assert.False(index.MentionsName("Missing"));
    }
}
