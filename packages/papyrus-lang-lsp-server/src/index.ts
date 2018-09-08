import { createConnection, InitializeResult, ProposedFeatures, TextDocuments } from 'vscode-languageserver';
import { LanguageService } from './LanguageService';

const connection = createConnection(ProposedFeatures.all);
const textDocuments = new TextDocuments();
textDocuments.listen(connection);

connection.onInitialize(({ capabilities }) => {
    const service = new LanguageService(connection, textDocuments, capabilities);
    connection.console.info(`Created Papyrus language service: ${service}`);

    const response: InitializeResult = {
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
            // referencesProvider: true,
        },
    };

    connection.console.info(`Papyrus language service capabilities: ${JSON.stringify(response.capabilities, null, 4)}`);

    return response;
});

connection.listen();
