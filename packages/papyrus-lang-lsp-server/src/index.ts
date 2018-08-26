// Arbitrary change.

import {
    isDescendentOfNodeOrSelf,
    visitAncestors,
    visitTree,
} from 'papyrus-lang/lib/common/TreeNode';
import { LookupFlags, MemberTypes } from 'papyrus-lang/lib/types/TypeChecker';
import {
    CompletionItem,
    CompletionItemKind,
    createConnection,
    Definition,
    Diagnostic,
    FileChangeType,
    Location,
    MarkedString,
    MarkupKind,
    Position,
    ProposedFeatures,
    SignatureInformation,
    TextDocument,
    TextDocumentPositionParams,
    TextDocuments,
} from 'vscode-languageserver';
import URI from 'vscode-uri';
import {
    getCompletionItem,
    getStubScriptCompletionItem,
} from './features/Completions';
import { buildHoverText } from './features/Descriptions';
import { signatureInformationForFunctionSymbol } from './features/Signatures';
import {
    getDocumentSymbolTree,
    getSymbolInformation,
} from './features/Symbols';
import { ProjectManager } from './ProjectManager';
import { TextDocumentLanguageServiceHost } from './TextDocumentLanguageServiceHost';
import { getTextForRange, papyrusRangeToRange } from './Utilities';

const connection = createConnection(ProposedFeatures.all);

import * as fs from 'fs';
import * as os from 'os';
import { iterateMany } from 'papyrus-lang/lib/common/Utilities';
import {
    findNodeAtPosition,
    FunctionCallExpressionNode,
    Node,
    NodeKind,
} from 'papyrus-lang/lib/parser/Node';
import {
    FunctionSymbol,
    Symbol,
    SymbolKind,
} from 'papyrus-lang/lib/symbols/Symbol';

connection.onRequest((request) => {
    console.log(request);
});

const textDocuments = new TextDocuments();

let hasWorkspaceFolderCapability = false;

const languageServiceHost = new TextDocumentLanguageServiceHost(textDocuments);
const projectManager = new ProjectManager(languageServiceHost);

connection.onInitialize(({ capabilities }) => {
    hasWorkspaceFolderCapability =
        capabilities.workspace && !!capabilities.workspace.workspaceFolders;

    return {
        capabilities: {
            textDocumentSync: textDocuments.syncKind,
            documentSymbolProvider: true,
            definitionProvider: true,
            hoverProvider: true,
            completionProvider: {
                triggerCharacters: ['.'],
                resolveProvider: true,
            },
            signatureHelpProvider: {
                triggerCharacters: ['(', ','],
            },
            referencesProvider: true,
        },
    };
});

async function updateProjects(reloadExisting: boolean) {
    const folders = await connection.workspace.getWorkspaceFolders();
    projectManager.updateProjects(
        folders.map((f) => URI.parse(f.uri).fsPath),
        reloadExisting
    );

    textDocuments.all().forEach(updateDiagnostics);
}

const pendingUpdates = new Map<string, NodeJS.Timer>();

connection.onInitialized(() => {
    updateProjects(false);

    if (hasWorkspaceFolderCapability) {
        connection.workspace.onDidChangeWorkspaceFolders(async () => {
            await updateProjects(false);
        });
    }

    textDocuments.onDidSave(async (change) => {
        if (change.document.languageId === 'papyrus-project') {
            await updateProjects(true);
        }
    });

    textDocuments.onDidChangeContent((change) => {
        if (pendingUpdates.has(change.document.uri)) {
            clearTimeout(pendingUpdates.get(change.document.uri));
        }

        pendingUpdates.set(
            change.document.uri,
            setTimeout(() => {
                try {
                    updateDiagnostics(change.document);
                } finally {
                    pendingUpdates.delete(change.document.uri);
                }
            }, 1000)
        );
    });
});

function updateDiagnostics(document: TextDocument) {
    const diagnostics = Array.from(
        iterateMany<Diagnostic>(
            projectManager.projectHosts.map((host) =>
                host.getDiagnosticsForDocument(document)
            )
        )
    );

    connection.sendDiagnostics({
        uri: document.uri,
        diagnostics,
    });
}

function getScriptNode(documentUri: string) {
    const scriptFile = projectManager.getScriptFileByUri(documentUri);

    if (!scriptFile) {
        return null;
    }

    return scriptFile.scriptNode.scriptNode;
}

function getNodeAtPosition(documentUri: string, position: Position) {
    const textDocument = languageServiceHost.getTextDocument(documentUri);
    const scriptNode = getScriptNode(textDocument.uri);

    if (!scriptNode) {
        return null;
    }

    return findNodeAtPosition(scriptNode, textDocument.offsetAt(position));
}

connection.onDocumentSymbol((params) => {
    const scriptNode = getScriptNode(params.textDocument.uri);

    // return getDocumentSymbolTree(
    //     scriptNode.symbol,
    //     languageServiceHost.getTextDocument(params.textDocument.uri)
    // ) as any;

    return Array.from(visitTree<Symbol>(scriptNode.symbol))
        .map((n) =>
            getSymbolInformation(
                n.node,
                languageServiceHost.getTextDocument(params.textDocument.uri)
            )
        )
        .filter((symbolInfo) => !!symbolInfo);
});

connection.onDefinition((params) => {
    const nodeAtPosition = getNodeAtPosition(
        params.textDocument.uri,
        params.position
    );

    if (nodeAtPosition) {
        for (const ancestor of visitAncestors<Node>(nodeAtPosition, true)) {
            if (ancestor.kind === NodeKind.Identifier) {
                const {
                    symbols,
                } = nodeAtPosition.script.scriptFile.program.typeChecker.getSymbolsForIdentifier(
                    ancestor
                );

                if (symbols.length > 0) {
                    const symbol = symbols[0];
                    if (symbol.kind === SymbolKind.Intrinsic) {
                        return null;
                    }

                    if (
                        isDescendentOfNodeOrSelf(
                            nodeAtPosition,
                            symbol.declaration.node
                        )
                    ) {
                        return null;
                    }

                    if (!symbol.declaration.node.script.scriptFile) {
                        return null;
                    }

                    return {
                        range: papyrusRangeToRange(
                            languageServiceHost.getTextDocument(
                                symbol.declaration.node.script.scriptFile.uri
                            ),
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
});

connection.onHover((params) => {
    const nodeAtPosition = getNodeAtPosition(
        params.textDocument.uri,
        params.position
    );

    if (nodeAtPosition) {
        for (const ancestor of visitAncestors<Node>(nodeAtPosition, true)) {
            if (ancestor.kind === NodeKind.Identifier) {
                const {
                    symbols,
                } = nodeAtPosition.script.scriptFile.program.typeChecker.getSymbolsForIdentifier(
                    ancestor
                );

                if (symbols.length > 0) {
                    const symbol = symbols[0];
                    if (symbol.kind === SymbolKind.Intrinsic) {
                        return null;
                    }

                    const text = buildHoverText(
                        symbol,
                        nodeAtPosition.script.scriptFile.program
                            .displayTextEmitter
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
});

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
        ? MemberTypes.Function |
              MemberTypes.Property |
              MemberTypes.Variable |
              MemberTypes.Struct
        : MemberTypes.Struct;
}

connection.onReferences((params) => {
    const nodeAtPosition = getNodeAtPosition(
        params.textDocument.uri,
        params.position
    );

    if (nodeAtPosition) {
        for (const ancestor of visitAncestors<Node>(nodeAtPosition, true)) {
            if (ancestor.kind === NodeKind.Identifier) {
                const {
                    symbols,
                } = nodeAtPosition.script.scriptFile.program.typeChecker.getSymbolsForIdentifier(
                    ancestor
                );

                if (symbols.length > 0) {
                    const symbol = symbols[0];

                    if (symbol.kind === SymbolKind.Intrinsic) {
                        return null;
                    }

                    const scriptFile =
                        symbol.declaration.node.script.scriptFile;
                    const program = scriptFile.program;

                    const referencingScripts = program.referenceResolver.getDirectReferencingScriptFiles(
                        scriptFile.scriptName
                    );

                    return Array.from(
                        iterateMany<Location>(
                            referencingScripts.map((referencingScript) => {
                                const references: Location[] = [];

                                if (
                                    !referencingScript.scriptNode.scriptNode.identifiers.has(
                                        symbol.name.toLowerCase()
                                    )
                                ) {
                                    return references;
                                }

                                for (const node of visitTree<Node>(
                                    referencingScript.scriptNode.scriptNode
                                )) {
                                    if (
                                        node.node.kind === NodeKind.Identifier
                                    ) {
                                        if (
                                            program.typeChecker
                                                .getSymbolsForIdentifier(
                                                    node.node
                                                )
                                                .symbols.some(
                                                    (s) => s === symbol
                                                )
                                        ) {
                                            references.push({
                                                range: papyrusRangeToRange(
                                                    languageServiceHost.getTextDocument(
                                                        referencingScript.uri
                                                    ),
                                                    node.node.range
                                                ),
                                                uri: referencingScript.uri,
                                            });
                                        }
                                    }
                                }

                                return references;
                            })
                        )
                    );
                }

                return null;
            }
        }
    }

    return null;
});

connection.onCompletion((params: TextDocumentPositionParams) => {
    const nodeAtPosition = getNodeAtPosition(
        params.textDocument.uri,
        params.position
    );

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

    const availableItems = availableSymbols.symbols.map((s) =>
        getCompletionItem(s, program.displayTextEmitter)
    );

    if (!availableSymbols.baseExpression) {
        // If this isn't for member access, we can assume that all scripts are available.
        availableItems.push(
            ...program.scriptNames.map((name) =>
                getStubScriptCompletionItem(name, program)
            )
        );

        availableItems.push(
            ...[
                'none',
                'true',
                'false',
                'int',
                'float',
                'bool',
                'string',
                'var',
                // 'If',
                // 'Else',
                // 'ElseIf',
                // 'Property',
                // 'Struct',
                // 'Group',
                // 'State',
                // 'Event',
                // 'Function',
                // 'EndIf',
                // 'EndProperty',
                // 'EndStruct',
                // 'EndGroup',
                // 'EndState',
                // 'EndEvent',
                // 'EndFunction',
            ].map((key) => ({
                label: key,
                kind: CompletionItemKind.Keyword,
            }))
        );
    }

    return availableItems;
});

connection.onCompletionResolve((item) => {
    if (!item.data || !item.data.isScriptStub) {
        return item;
    }

    const projectHost = projectManager.projectHosts.find(
        (host) => host.program.project.filePath === item.data.projectFile
    );

    const type = projectHost.program.getTypeForName(item.data.scriptName);

    if (!type) {
        return item;
    }

    return getCompletionItem(
        type.symbol,
        projectHost.program.displayTextEmitter
    );
});

connection.onSignatureHelp((params) => {
    const nodeAtPosition = getNodeAtPosition(
        params.textDocument.uri,
        params.position
    );

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

        const { symbols } = typeChecker.getSymbolsForIdentifier(
            callExpression.identifier
        );

        if (symbols.length === 0) {
            return null;
        }

        const functionSymbol = symbols[0] as FunctionSymbol;

        const currentParameter =
            nodeAtPosition.kind === NodeKind.FunctionCallExpressionParameter
                ? nodeAtPosition
                : null;

        const activeParameterIndex =
            functionSymbol.parameters.length > 0
                ? currentParameter
                    ? callExpression.parameters.indexOf(currentParameter)
                    : 0
                : null;

        return {
            activeParameter: activeParameterIndex,
            activeSignature: 0,
            signatures: [
                signatureInformationForFunctionSymbol(
                    functionSymbol,
                    program.displayTextEmitter
                ),
            ],
        };
    }

    return null;
});

connection.onDidChangeWatchedFiles(async (changes) => {
    await updateProjects(false);
});

// TODO: Update diagnostics for background files.
textDocuments.onDidClose((e) => {
    connection.sendDiagnostics({ uri: e.document.uri, diagnostics: [] });
});

textDocuments.listen(connection);

connection.listen();
