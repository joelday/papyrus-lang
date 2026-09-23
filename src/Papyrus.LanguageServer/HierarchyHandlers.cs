using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

/// <summary>Where a symbol is declared: its file, whole declaration, and name.</summary>
internal readonly record struct SymbolLocation(string Path, SyntaxTree Tree, TextSpan Span, TextSpan NameSpan)
{
    public string Uri => LspUris.ToUri(Path);

    public Location ToLocation() =>
        new() { Uri = Uri, Range = DocumentSymbolHandler.ToRange(Tree.GetText().Lines, NameSpan) };
}

internal static class HierarchySymbols
{
    /// <summary>
    /// Locates <paramref name="symbol"/>'s declaration. A declaration in another script is
    /// found through the script that owns it, so it works for files that are not open.
    /// </summary>
    internal static bool TryLocate(
        LspSnapshot snapshot,
        ProjectId projectId,
        ISymbol symbol,
        out SymbolLocation location
    )
    {
        location = default;
        if (symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty
            || symbol.DeclaringSyntaxReferences[0] is not { SyntaxTree: { } tree } reference)
        {
            return false;
        }

        var owner = symbol as IScriptSymbol ?? symbol.ContainingScript;
        if (owner is null || !snapshot.Solution.TryGetScriptPath(projectId, owner.QualifiedName, out var path))
        {
            return false;
        }

        location = new SymbolLocation(path, tree, reference.Span, SymbolDisplay.GetNameSpan(symbol) ?? reference.Span);
        return true;
    }

    /// <summary>The symbol named at a request's position, with the document's project.</summary>
    internal static bool TryGetSymbol(
        LspSnapshot snapshot,
        TextDocumentIdentifier? document,
        Position? position,
        CancellationToken ct,
        out ISymbol symbol,
        out SemanticModel model,
        out ProjectId projectId
    )
    {
        symbol = null!;
        if (position is null
            || !LspDocuments.TryGetSemanticModel(snapshot, document, out model, out var tree, out _, out projectId))
        {
            model = null!;
            projectId = null!;
            return false;
        }

        var offset = tree.GetText().Lines.GetOffset(position.Line, position.Character);
        symbol = model.GetSymbolInfo(offset, ct)!;
        return symbol is not null;
    }

    /// <summary>Re-resolves an item from its data (the uri and offset of its name).</summary>
    internal static bool TryResolve(
        LspSnapshot snapshot,
        HierarchyItem? item,
        CancellationToken ct,
        out ISymbol symbol,
        out ProjectId projectId
    )
    {
        symbol = null!;
        projectId = null!;
        if (item?.Data is not { } data
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                new TextDocumentIdentifier { Uri = data.Uri },
                out var model,
                out _,
                out _,
                out projectId))
        {
            return false;
        }

        symbol = model.GetSymbolInfo(data.Offset, ct)!;
        return symbol is not null;
    }

    internal static HierarchyItem ToItem(ISymbol symbol, SymbolLocation location)
    {
        var lineMap = location.Tree.GetText().Lines;
        return new HierarchyItem
        {
            Name = symbol.Name.Text,
            Kind = symbol switch
            {
                IScriptSymbol => 5, // Class
                IStructSymbol => 23, // Struct
                IEventSymbol => 24, // Event
                _ => 6, // Method
            },
            Detail = symbol is IScriptSymbol script
                ? script.BaseType is { TypeKind: not TypeKind.Error } baseType ? "extends " + baseType.Name.Text : null
                : symbol.ContainingScript?.Name.Text,
            Uri = location.Uri,
            Range = DocumentSymbolHandler.ToRange(lineMap, location.Span),
            SelectionRange = DocumentSymbolHandler.ToRange(lineMap, location.NameSpan),
            Data = new HierarchyItemData { Uri = location.Uri, Offset = location.NameSpan.Start },
        };
    }

    internal static HierarchyItem[]? Prepare(LspSnapshot snapshot, ProjectId projectId, ISymbol? symbol) =>
        symbol is not null && TryLocate(snapshot, projectId, symbol, out var location)
            ? [ToItem(symbol, location)]
            : null;

    internal static Range[] ToRanges(SyntaxTree tree, IEnumerable<TextSpan> spans)
    {
        var lineMap = tree.GetText().Lines;
        var ranges = new List<Range>();
        foreach (var span in spans)
        {
            ranges.Add(DocumentSymbolHandler.ToRange(lineMap, span));
        }

        return ranges.ToArray();
    }
}

/// <summary><c>textDocument/typeDefinition</c>: the script or struct a symbol's type names.</summary>
internal sealed class TypeDefinitionHandler : ILspHandler<DefinitionParams?, Location[]>
{
    internal const string MethodName = "textDocument/typeDefinition";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<Location[]> HandleAsync(DefinitionParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!HierarchySymbols.TryGetSymbol(snapshot, p?.TextDocument, p?.Position, ct, out var symbol, out _, out var projectId))
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        var type = symbol switch
        {
            ITypeSymbol self => self,
            IPropertySymbol property => property.Type,
            IVariableSymbol variable => variable.Type,
            IParameterSymbol parameter => parameter.Type,
            IFunctionSymbol function => function.ReturnType,
            _ => null,
        };
        if (type?.GetArrayElementType() is { } element)
        {
            type = element;
        }

        return Task.FromResult(
            type is IScriptSymbol or IStructSymbol && HierarchySymbols.TryLocate(snapshot, projectId, type, out var location)
                ? new[] { location.ToLocation() }
                : Array.Empty<Location>()
        );
    }
}

/// <summary>
/// <c>textDocument/implementation</c>: the scripts extending a script, or the overrides of
/// a function or event in them.
/// </summary>
internal sealed class ImplementationHandler : ILspHandler<DefinitionParams?, Location[]>
{
    internal const string MethodName = "textDocument/implementation";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<Location[]> HandleAsync(DefinitionParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!HierarchySymbols.TryGetSymbol(snapshot, p?.TextDocument, p?.Position, ct, out var symbol, out _, out var projectId))
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        var locations = new List<Location>();
        foreach (var match in snapshot.Solution.FindImplementations(projectId, symbol, ct))
        {
            locations.Add(
                new Location
                {
                    Uri = LspUris.ToUri(match.Path),
                    Range = DocumentSymbolHandler.ToRange(match.Tree.GetText().Lines, match.Declaration.NameSpan),
                }
            );
        }

        return Task.FromResult(locations.ToArray());
    }
}

internal sealed class PrepareCallHierarchyHandler : ILspHandler<TextDocumentPositionParams?, HierarchyItem[]?>
{
    internal const string MethodName = "textDocument/prepareCallHierarchy";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<HierarchyItem[]?> HandleAsync(TextDocumentPositionParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            HierarchySymbols.TryGetSymbol(snapshot, p?.TextDocument, p?.Position, ct, out var symbol, out _, out var projectId)
                ? HierarchySymbols.Prepare(snapshot, projectId, symbol as IFunctionSymbol)
                : null
        );
    }
}

/// <summary><c>callHierarchy/incomingCalls</c>: the functions calling the item, and where.</summary>
internal sealed class IncomingCallsHandler : ILspHandler<HierarchyItemParams?, CallHierarchyIncomingCall[]?>
{
    internal const string MethodName = "callHierarchy/incomingCalls";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<CallHierarchyIncomingCall[]?> HandleAsync(HierarchyItemParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!HierarchySymbols.TryResolve(snapshot, p?.Item, ct, out var symbol, out var projectId)
            || symbol is not IFunctionSymbol function)
        {
            return Task.FromResult<CallHierarchyIncomingCall[]?>(null);
        }

        var calls = new List<CallHierarchyIncomingCall>();
        foreach (var (caller, path, tree, spans) in snapshot.Solution.FindCallers(projectId, function, ct))
        {
            if (caller.DeclaringSyntaxReferences.IsDefaultOrEmpty)
            {
                continue;
            }

            var span = caller.DeclaringSyntaxReferences[0].Span;
            var location = new SymbolLocation(path, tree, span, SymbolDisplay.GetNameSpan(caller) ?? span);
            calls.Add(
                new CallHierarchyIncomingCall
                {
                    From = HierarchySymbols.ToItem(caller, location),
                    FromRanges = HierarchySymbols.ToRanges(tree, spans),
                }
            );
        }

        return Task.FromResult<CallHierarchyIncomingCall[]?>(calls.ToArray());
    }
}

/// <summary><c>callHierarchy/outgoingCalls</c>: the functions the item calls, and where.</summary>
internal sealed class OutgoingCallsHandler : ILspHandler<HierarchyItemParams?, CallHierarchyOutgoingCall[]?>
{
    internal const string MethodName = "callHierarchy/outgoingCalls";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<CallHierarchyOutgoingCall[]?> HandleAsync(HierarchyItemParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (p?.Item?.Data is not { } data
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                new TextDocumentIdentifier { Uri = data.Uri },
                out var model,
                out var tree,
                out _,
                out var projectId)
            || model.GetSymbolInfo(data.Offset, ct) is not IFunctionSymbol function)
        {
            return Task.FromResult<CallHierarchyOutgoingCall[]?>(null);
        }

        // One entry per target, in order of first call; calls into scripts without source
        // (or into intrinsics) have nowhere to navigate and are left out.
        var byTarget = new Dictionary<(string, int), (HierarchyItem Item, List<TextSpan> Spans)>();
        var order = new List<(string, int)>();
        foreach (var (target, span) in model.GetOutgoingCalls(function, ct))
        {
            if (!HierarchySymbols.TryLocate(snapshot, projectId, target, out var location))
            {
                continue;
            }

            var key = (location.Path, location.NameSpan.Start);
            if (!byTarget.TryGetValue(key, out var entry))
            {
                entry = (HierarchySymbols.ToItem(target, location), new List<TextSpan>());
                byTarget[key] = entry;
                order.Add(key);
            }

            entry.Spans.Add(span);
        }

        var calls = new List<CallHierarchyOutgoingCall>();
        foreach (var key in order)
        {
            var (item, spans) = byTarget[key];
            calls.Add(new CallHierarchyOutgoingCall { To = item, FromRanges = HierarchySymbols.ToRanges(tree, spans) });
        }

        return Task.FromResult<CallHierarchyOutgoingCall[]?>(calls.ToArray());
    }
}

internal sealed class PrepareTypeHierarchyHandler : ILspHandler<TextDocumentPositionParams?, HierarchyItem[]?>
{
    internal const string MethodName = "textDocument/prepareTypeHierarchy";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<HierarchyItem[]?> HandleAsync(TextDocumentPositionParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            HierarchySymbols.TryGetSymbol(snapshot, p?.TextDocument, p?.Position, ct, out var symbol, out _, out var projectId)
                ? HierarchySymbols.Prepare(snapshot, projectId, symbol as IScriptSymbol)
                : null
        );
    }
}

internal sealed class SupertypesHandler : ILspHandler<HierarchyItemParams?, HierarchyItem[]?>
{
    internal const string MethodName = "typeHierarchy/supertypes";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<HierarchyItem[]?> HandleAsync(HierarchyItemParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            HierarchySymbols.TryResolve(snapshot, p?.Item, ct, out var symbol, out var projectId)
            && symbol is IScriptSymbol { BaseType: { TypeKind: not TypeKind.Error } baseType }
                ? HierarchySymbols.Prepare(snapshot, projectId, baseType)
                : null
        );
    }
}

internal sealed class SubtypesHandler : ILspHandler<HierarchyItemParams?, HierarchyItem[]?>
{
    internal const string MethodName = "typeHierarchy/subtypes";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<HierarchyItem[]?> HandleAsync(HierarchyItemParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!HierarchySymbols.TryResolve(snapshot, p?.Item, ct, out var symbol, out var projectId)
            || symbol is not IScriptSymbol script)
        {
            return Task.FromResult<HierarchyItem[]?>(null);
        }

        var compilation = snapshot.Solution.GetCompilation(projectId);
        var items = new List<HierarchyItem>();
        foreach (var (name, _) in snapshot.Solution.FindDerivedScripts(projectId, script, transitive: false, ct))
        {
            if (compilation.GetScript(name) is { TypeKind: not TypeKind.Error } derived
                && HierarchySymbols.TryLocate(snapshot, projectId, derived, out var location))
            {
                items.Add(HierarchySymbols.ToItem(derived, location));
            }
        }

        items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<HierarchyItem[]?>(items.ToArray());
    }
}
