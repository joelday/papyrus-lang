import { visitAncestors } from 'papyrus-lang/lib/common/TreeNode';
import { Node, NodeKind } from 'papyrus-lang/lib/parser/Node';
import { SymbolKind } from 'papyrus-lang/lib/symbols/Symbol';
import { CancellationToken, MarkupKind, TextDocumentPositionParams } from 'vscode-languageserver';
import { IDocumentHelpers } from '../DocumentHelpers';
import { buildHoverText } from '../features/Descriptions';
import { Handler } from '../RequestHandlerService';

export class HoverHandler implements Handler<'onHover'> {
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

                        const text = buildHoverText(
                            symbol,
                            nodeAtPosition.script.scriptFile.program.displayTextEmitter
                        );

                        if (!text) {
                            return null;
                        }

                        return {
                            contents: {
                                kind: MarkupKind.Markdown,
                                value: text,
                            },
                        };
                    }

                    return null;
                }
            }
        }

        return null;
    }
}
