import { isDescendentOfNodeOrSelf, visitAncestors } from 'papyrus-lang/lib/common/TreeNode';
import { Node, NodeKind } from 'papyrus-lang/lib/parser/Node';
import { SymbolKind } from 'papyrus-lang/lib/symbols/Symbol';
import { CancellationToken, TextDocumentPositionParams } from 'vscode-languageserver';
import { IDocumentHelpers } from '../DocumentHelpers';
import { Handler } from '../RequestHandlerService';
import { papyrusRangeToRange } from '../Utilities';

export class DefinitionHandler implements Handler<'onDefinition'> {
    private readonly _documentHelpers: IDocumentHelpers;

    constructor(@IDocumentHelpers documentHelpers: IDocumentHelpers) {
        this._documentHelpers = documentHelpers;
    }

    public handleRequest(params: TextDocumentPositionParams, _cancellationToken: CancellationToken) {
        const nodeAtPosition = this._documentHelpers.getNodeAtPosition(params.textDocument.uri, params.position);

        if (nodeAtPosition) {
            for (const ancestor of visitAncestors<Node>(nodeAtPosition, true)) {
                if (ancestor.kind === NodeKind.Identifier) {
                    const { symbols } = nodeAtPosition.script.scriptFile.program.typeChecker.getSymbolsForIdentifier(
                        ancestor
                    );

                    if (symbols.length > 0) {
                        const symbol = symbols[0];
                        if (symbol.kind === SymbolKind.Intrinsic) {
                            return null;
                        }

                        if (isDescendentOfNodeOrSelf(nodeAtPosition, symbol.declaration.node)) {
                            return null;
                        }

                        if (!symbol.declaration.node.script.scriptFile) {
                            return null;
                        }

                        return {
                            range: papyrusRangeToRange(
                                this._documentHelpers.getTextDocument(symbol.declaration.node.script.scriptFile.uri),
                                symbol.declaration.identifier.range
                            ),
                            uri: symbol.declaration.node.script.scriptFile.uri,
                        };
                    }

                    return null;
                }
            }
        }

        return null;
    }
}
