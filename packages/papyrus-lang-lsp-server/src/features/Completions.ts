import { DisplayTextEmitter } from 'papyrus-lang/lib/program/DisplayTextEmitter';
import { Program } from 'papyrus-lang/lib/program/Program';
import { Symbol, SymbolKind as PapyrusSymbolKind } from 'papyrus-lang/lib/symbols/Symbol';
import { TypeChecker } from 'papyrus-lang/lib/types/TypeChecker';
import { CompletionItem, CompletionItemKind, SymbolInformation, TextDocument } from 'vscode-languageserver';

function getCompletionItemKind(symbol: Symbol) {
    switch (symbol.kind) {
        case PapyrusSymbolKind.Function:
            return CompletionItemKind.Method;
        case PapyrusSymbolKind.Property:
            return CompletionItemKind.Property;
        case PapyrusSymbolKind.Script:
            return CompletionItemKind.Class;
        case PapyrusSymbolKind.Struct:
            return CompletionItemKind.Struct;
        case PapyrusSymbolKind.Event:
            return CompletionItemKind.Event;
        case PapyrusSymbolKind.Variable:
            if (symbol.parent.kind === PapyrusSymbolKind.Script || symbol.parent.kind === PapyrusSymbolKind.Struct) {
                return CompletionItemKind.Field;
            }

            return CompletionItemKind.Variable;
        default:
            return null;
    }
}

export function getStubScriptCompletionItem(scriptName: string, program: Program): CompletionItem {
    return {
        label: scriptName,
        kind: CompletionItemKind.Class,
        data: {
            isScriptStub: true,
            scriptName,
            projectFile: program.project.filePath,
        },
        sortText: `α${scriptName}`,
    };
}

export function getCompletionItem(symbol: Symbol, displayTextEmitter: DisplayTextEmitter): CompletionItem {
    if (!symbol || symbol.kind === PapyrusSymbolKind.Intrinsic) {
        return null;
    }

    const displayText = displayTextEmitter.getDisplayText(symbol);

    return {
        label: symbol.name,
        data: { isScriptStub: false },
        documentation: displayText.documentation,
        kind: getCompletionItemKind(symbol),
        detail: displayText.text,
        sortText: (symbol.kind === PapyrusSymbolKind.Script ? 'α' : '') + symbol.name,
    };
}
