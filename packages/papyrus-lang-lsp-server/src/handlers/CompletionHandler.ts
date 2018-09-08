import { isDescendentOfNodeOrSelf, visitAncestors } from 'papyrus-lang/lib/common/TreeNode';
import { Node, NodeKind } from 'papyrus-lang/lib/parser/Node';
import { SymbolKind } from 'papyrus-lang/lib/symbols/Symbol';
import { LookupFlags, MemberTypes } from 'papyrus-lang/lib/types/TypeChecker';
import { CancellationToken, CompletionItemKind, TextDocumentPositionParams } from 'vscode-languageserver';
import { IDocumentHelpers } from '../DocumentHelpers';
import { getCompletionItem, getStubScriptCompletionItem } from '../features/Completions';
import { Handler } from '../RequestHandlerService';
import { papyrusRangeToRange } from '../Utilities';

function getNodeIsBlockScoped(node: Node) {
    // TODO: Fix this based on hierarchy.
    switch (node.kind) {
        case NodeKind.Script:
        case NodeKind.StateDefinition:
        case NodeKind.GroupDefinition:
        case NodeKind.PropertyDefinition:
        case NodeKind.ScriptHeader:
        case NodeKind.VariableDefinition:
        case NodeKind.Import:
            return false;
        default:
            return true;
    }
}

function getValidMemberTypesForChild(node: Node): MemberTypes {
    return getNodeIsBlockScoped(node)
        ? MemberTypes.Function | MemberTypes.Property | MemberTypes.Variable | MemberTypes.Struct
        : MemberTypes.Struct;
}

export class CompletionHandler implements Handler<'onCompletion'> {
    private readonly _documentHelpers: IDocumentHelpers;

    constructor(@IDocumentHelpers documentHelpers: IDocumentHelpers) {
        this._documentHelpers = documentHelpers;
    }

    public handleRequest(params: TextDocumentPositionParams, _cancellationToken: CancellationToken) {
        const nodeAtPosition = this._documentHelpers.getNodeAtPosition(params.textDocument.uri, params.position);

        if (nodeAtPosition.script.scriptFile) {
            const textDocument = this._documentHelpers.getTextDocument(params.textDocument.uri);
            const documentPosition = textDocument.offsetAt(params.position);

            for (const token of nodeAtPosition.script.scriptFile.tokens.tokens) {
                if (token.range.start <= documentPosition && token.range.end >= documentPosition && token.isComment) {
                    return [];
                }

                if (token.range.start > documentPosition) {
                    break;
                }
            }
        }

        const memberTypes = getValidMemberTypesForChild(nodeAtPosition);

        // TODO: Getting the type checker from an arbitrary node is bonkers:
        const program = nodeAtPosition.script.scriptFile.program;
        const typeChecker = program.typeChecker;

        const availableSymbols = typeChecker.getAvailableSymbolsAtNode(
            nodeAtPosition,
            memberTypes,
            (LookupFlags.Default | LookupFlags.FlattenHierarchy) ^
                (getNodeIsBlockScoped(nodeAtPosition) ? 0 : LookupFlags.Instance)
        );

        const availableItems = availableSymbols.symbols.map((s) => getCompletionItem(s, program.displayTextEmitter));

        if (!availableSymbols.baseExpression) {
            // If this isn't for member access, we can assume that all scripts are available.
            availableItems.push(...program.scriptNames.map((name) => getStubScriptCompletionItem(name, program)));

            availableItems.push(
                ...['none', 'true', 'false', 'int', 'float', 'bool', 'string', 'var'].map((key) => ({
                    label: key,
                    kind: CompletionItemKind.Keyword,
                }))
            );
        }

        return availableItems;
    }
}
