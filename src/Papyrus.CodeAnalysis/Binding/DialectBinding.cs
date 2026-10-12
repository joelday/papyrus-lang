using System;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Syntax.InternalSyntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Binding;

internal static class DialectBinding
{
    internal readonly struct Feature
    {
        public Feature(SyntaxKind kind, Func<PapyrusDialect, bool> isSupported, DiagnosticDescriptor descriptor)
        {
            Kind = kind;
            IsSupported = isSupported;
            Descriptor = descriptor;
        }

        public SyntaxKind Kind { get; }

        public Func<PapyrusDialect, bool> IsSupported { get; }

        public DiagnosticDescriptor Descriptor { get; }
    }

    // Syntax that exists only when a dialect flag is on; bind still rejects it
    // if a tree from another dialect (or a recovered name) is attached here.
    internal static readonly Feature[] Features =
    [
        new(SyntaxKind.StructDeclaration, static d => d.SupportsStructs, DiagnosticCatalog.UnsupportedStruct),
        new(SyntaxKind.HashToken, static d => d.SupportsStructs, DiagnosticCatalog.UnsupportedStruct),
        new(SyntaxKind.GroupDeclaration, static d => d.SupportsGroups, DiagnosticCatalog.UnsupportedGroup),
        new(SyntaxKind.CustomEventDeclaration, static d => d.SupportsCustomEvents, DiagnosticCatalog.UnsupportedCustomEvent),
        new(SyntaxKind.GuardDeclaration, static d => d.SupportsGuards, DiagnosticCatalog.UnsupportedGuard),
        new(SyntaxKind.GuardStatement, static d => d.SupportsGuards, DiagnosticCatalog.UnsupportedGuard),
        new(SyntaxKind.TryGuardStatement, static d => d.SupportsGuards, DiagnosticCatalog.UnsupportedGuard),
        new(SyntaxKind.QualifiedName, static d => d.SupportsNamespaces, DiagnosticCatalog.UnsupportedNamespace),
        new(SyntaxKind.IsKeyword, static d => d.SupportsIsOperator, DiagnosticCatalog.InvalidIsOperator),
    ];

    public static ImmutableArray<Diagnostic> Collect(SyntaxTree tree, PapyrusDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(dialect);

        if (!tree.TryGetRoot(out var root))
        {
            return ImmutableArray<Diagnostic>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        Walk(root.Green, position: 0, dialect, tree, builder);
        return builder.ToImmutable();
    }

    private static void Walk(
        GreenNode node,
        int position,
        PapyrusDialect dialect,
        SyntaxTree tree,
        ImmutableArray<Diagnostic>.Builder builder)
    {
        if (TryMatch(node.Kind, dialect, out var descriptor))
        {
            builder.Add(new Diagnostic(new DiagnosticInfo(descriptor), NodeSpan(node, position), tree));
            if (node.Kind == SyntaxKind.QualifiedName)
            {
                return;
            }
        }

        if (node is GreenToken)
        {
            return;
        }

        var offset = 0;
        var slotCount = node.SlotCount;
        for (var i = 0; i < slotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child is null)
            {
                continue;
            }

            Walk(child, position + offset, dialect, tree, builder);
            offset += child.FullWidth;
        }
    }

    private static bool TryMatch(SyntaxKind kind, PapyrusDialect dialect, out DiagnosticDescriptor descriptor)
    {
        for (var i = 0; i < Features.Length; i++)
        {
            var feature = Features[i];
            if (feature.Kind == kind && !feature.IsSupported(dialect))
            {
                descriptor = feature.Descriptor;
                return true;
            }
        }

        descriptor = null!;
        return false;
    }

    private static TextSpan NodeSpan(GreenNode node, int position)
    {
        var leading = node.GetLeadingTriviaWidth();
        var trailing = node.GetTrailingTriviaWidth();
        var length = node.FullWidth - leading - trailing;
        return new TextSpan(position + leading, length < 0 ? 0 : length);
    }
}
