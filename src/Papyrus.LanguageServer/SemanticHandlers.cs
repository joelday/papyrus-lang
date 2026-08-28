using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.LanguageServer;

internal sealed class HoverHandler : ILspHandler<TextDocumentPositionParams?, Hover?>
{
    internal const string MethodName = "textDocument/hover";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<Hover?> HandleAsync(
        TextDocumentPositionParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult<Hover?>(null);
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        var symbol = model.GetSymbolInfo(offset, ct);
        if (symbol is null)
        {
            return Task.FromResult<Hover?>(null);
        }

        return Task.FromResult<Hover?>(new Hover { Contents = [FormatSymbol(symbol)] });
    }

    private static string FormatSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            IFunctionSymbol function =>
                $"{function.Name.Text}({string.Join(", ", Parameters(function))}) -> {function.ReturnType.Name.Text}",
            IPropertySymbol property => $"{property.Type.Name.Text} {property.Name.Text}",
            IVariableSymbol variable => $"{variable.Type.Name.Text} {variable.Name.Text}",
            IParameterSymbol parameter => $"{parameter.Type.Name.Text} {parameter.Name.Text}",
            _ => $"{symbol.Kind} {symbol.Name.Text}",
        };
    }

    private static IEnumerable<string> Parameters(IFunctionSymbol function)
    {
        foreach (var parameter in function.Parameters)
        {
            yield return $"{parameter.Type.Name.Text} {parameter.Name.Text}";
        }
    }
}

internal sealed class DefinitionHandler : ILspHandler<DefinitionParams?, Location[]>
{
    internal const string MethodName = "textDocument/definition";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<Location[]> HandleAsync(
        DefinitionParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out var path
            )
        )
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        var symbol = model.GetSymbolInfo(offset, ct);
        if (symbol is null)
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        var lineMap = tree.GetText().Lines;
        var locations = new List<Location>();
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == tree)
            {
                locations.Add(
                    new Location
                    {
                        Uri = p.TextDocument?.Uri ?? LspTestUri(path),
                        Range = DocumentSymbolHandler.ToRange(lineMap, reference.Span),
                    }
                );
            }
        }

        return Task.FromResult(locations.ToArray());
    }

    private static string LspTestUri(string path) => new Uri(path).AbsoluteUri;
}

internal sealed class CompletionHandler : ILspHandler<CompletionParams?, CompletionItem[]>
{
    internal const string MethodName = "textDocument/completion";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<CompletionItem[]> HandleAsync(
        CompletionParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult(Array.Empty<CompletionItem>());
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        var container = model.TryGetMemberAccessContainer(offset, out var containerType, ct)
            ? containerType
            : null;
        var symbols = model.LookupSymbols(offset, container);

        var items = new CompletionItem[symbols.Length];
        for (var i = 0; i < symbols.Length; i++)
        {
            items[i] = ToCompletionItem(symbols[i]);
        }

        return Task.FromResult(items);
    }

    private static CompletionItem ToCompletionItem(ISymbol symbol)
    {
        return new CompletionItem
        {
            Label = symbol.Name.Text,
            Kind = ToLspKind(symbol),
            Detail = symbol switch
            {
                IFunctionSymbol function => $"{function.ReturnType.Name.Text}",
                IPropertySymbol property => property.Type.Name.Text,
                IVariableSymbol variable => variable.Type.Name.Text,
                IParameterSymbol parameter => parameter.Type.Name.Text,
                _ => null,
            },
        };
    }

    private static int ToLspKind(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.Function or SymbolKind.Event => 3,
            SymbolKind.Property => 10,
            SymbolKind.Variable or SymbolKind.Parameter => 6,
            SymbolKind.Script => 7,
            SymbolKind.Struct => 22,
            _ => 1,
        };
    }
}

internal sealed class SignatureHelpHandler : ILspHandler<SignatureHelpParams?, SignatureHelp?>
{
    internal const string MethodName = "textDocument/signatureHelp";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<SignatureHelp?> HandleAsync(
        SignatureHelpParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        if (!model.TryGetInvocationContext(offset, out var function, out var activeParameter, ct))
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        var parameters = new ParameterInformation[function!.Parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = function.Parameters[i];
            parameters[i] = new ParameterInformation
            {
                Label = $"{parameter.Type.Name.Text} {parameter.Name.Text}",
            };
        }

        return Task.FromResult<SignatureHelp?>(
            new SignatureHelp
            {
                Signatures =
                [
                    new SignatureInformation
                    {
                        Label =
                            $"{function.Name.Text}({string.Join(", ", parameters.Select(x => x.Label))})",
                        Parameters = parameters,
                    },
                ],
                ActiveSignature = 0,
                ActiveParameter = Math.Min(activeParameter, Math.Max(parameters.Length - 1, 0)),
            }
        );
    }
}
