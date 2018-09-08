import { createConnection, ProposedFeatures, TextDocuments } from 'vscode-languageserver';
import { LanguageService } from './LanguageService';

const connection = createConnection(ProposedFeatures.all);
const textDocuments = new TextDocuments();
textDocuments.listen(connection);

connection.onInitialize(({ capabilities }) => {
    const service = new LanguageService(connection, textDocuments, capabilities);

    connection.onShutdown(async () => {
        await service.shutdown();
    });

    return {
        capabilities: {
            textDocumentSync: textDocuments.syncKind,
            documentSymbolProvider: true,
            definitionProvider: true,
            hoverProvider: true,
            workspace: {
                workspaceFolders: {
                    supported: true,
                    changeNotifications: true,
                },
            },
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

connection.listen();
