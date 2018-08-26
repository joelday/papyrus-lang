import {
    Symbol,
    SymbolKind as PapyrusSymbolKind,
} from 'papyrus-lang/lib/symbols/Symbol';
import {
    DocumentSymbol,
    SymbolInformation,
    SymbolKind,
    TextDocument,
} from 'vscode-languageserver';
import { papyrusRangeToRange } from '../Utilities';

function getSymbolKind(symbol: Symbol) {
    switch (symbol.kind) {
        case PapyrusSymbolKind.CustomEvent:
            return SymbolKind.Event;
        case PapyrusSymbolKind.Event:
            return SymbolKind.Event;
        case PapyrusSymbolKind.Function:
            return SymbolKind.Method;
        case PapyrusSymbolKind.Parameter:
            return SymbolKind.Variable;
        case PapyrusSymbolKind.Property:
            return SymbolKind.Property;
        case PapyrusSymbolKind.Script:
            return SymbolKind.Class;
        case PapyrusSymbolKind.State:
            return SymbolKind.Namespace;
        case PapyrusSymbolKind.Group:
            return SymbolKind.Namespace;
        case PapyrusSymbolKind.Struct:
            return SymbolKind.Struct;
        case PapyrusSymbolKind.Variable:
            if (symbol.parent.kind === PapyrusSymbolKind.Script) {
                return SymbolKind.Field;
            }

            return SymbolKind.Variable;
        case PapyrusSymbolKind.Import:
            return SymbolKind.Class;
        default:
            return null;
    }
}

export function getSymbolInformation(
    symbol: Symbol,
    textDocument: TextDocument
): SymbolInformation {
    if (!symbol || symbol.kind === PapyrusSymbolKind.Intrinsic) {
        return null;
    }

    const symbolKind = getSymbolKind(symbol);
    if (!symbolKind) {
        return null;
    }

    return {
        name: symbol.name,
        containerName: symbol.parent ? symbol.parent.name : null,
        kind: symbolKind,
        location: {
            uri: textDocument.uri,
            range: papyrusRangeToRange(
                textDocument,
                symbol.declaration.node.range
            ),
        },
    };
}

export function getDocumentSymbolTree(
    symbol: Symbol,
    textDocument: TextDocument
): DocumentSymbol {
    if (!symbol || symbol.kind === PapyrusSymbolKind.Intrinsic) {
        return null;
    }

    const symbolKind = getSymbolKind(symbol);
    if (!symbolKind) {
        return null;
    }

    return {
        name: symbol.name,
        kind: symbolKind,
        selectionRange: papyrusRangeToRange(
            textDocument,
            symbol.declaration.identifier.range
        ),
        range: papyrusRangeToRange(textDocument, symbol.declaration.node.range),
        children: symbol.children.map((child) =>
            getDocumentSymbolTree(child, textDocument)
        ),
    };
}
