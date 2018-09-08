import { FunctionCallExpressionNode, NodeKind } from 'papyrus-lang/lib/parser/Node';
import { FunctionSymbol } from 'papyrus-lang/lib/symbols/Symbol';
import { CancellationToken, TextDocumentPositionParams } from 'vscode-languageserver';
import { IDocumentHelpers } from '../DocumentHelpers';
import { signatureInformationForFunctionSymbol } from '../features/Signatures';
import { Handler } from '../RequestHandlerService';

export class SignatureHelpHandler implements Handler<'onSignatureHelp'> {
    private readonly _documentHelpers: IDocumentHelpers;

    constructor(@IDocumentHelpers documentHelpers: IDocumentHelpers) {
        this._documentHelpers = documentHelpers;
    }

    public handleRequest(params: TextDocumentPositionParams, _cancellationToken: CancellationToken) {
        const nodeAtPosition = this._documentHelpers.getNodeAtPosition(params.textDocument.uri, params.position);

        const program = nodeAtPosition.script.scriptFile.program;
        const typeChecker = program.typeChecker;

        if (
            nodeAtPosition.kind === NodeKind.FunctionCallExpression ||
            nodeAtPosition.kind === NodeKind.FunctionCallExpressionParameter
        ) {
            const callExpression =
                nodeAtPosition.kind === NodeKind.FunctionCallExpressionParameter
                    ? (nodeAtPosition.parent as FunctionCallExpressionNode)
                    : nodeAtPosition;

            const { symbols } = typeChecker.getSymbolsForIdentifier(callExpression.identifier);

            if (symbols.length === 0) {
                return null;
            }

            const functionSymbol = symbols[0] as FunctionSymbol;

            const currentParameter =
                nodeAtPosition.kind === NodeKind.FunctionCallExpressionParameter ? nodeAtPosition : null;

            const activeParameterIndex =
                functionSymbol.parameters.length > 0
                    ? currentParameter
                        ? callExpression.parameters.indexOf(currentParameter)
                        : 0
                    : null;

            return {
                activeParameter: activeParameterIndex,
                activeSignature: 0,
                signatures: [signatureInformationForFunctionSymbol(functionSymbol, program.displayTextEmitter)],
            };
        }

        return null;
    }
}
