import { ClientServer } from './ClientServer';
import { ExtensionContext, Disposable, TextEditorDecorationType, window, TextEditor, Range, Position, TextDocument, workspace, languages } from 'vscode';
import { ScriptStatusCodeLensProvider } from './ScriptStatusCodeLensProvider';
import { PapyrusExtension } from './PapyrusExtension';
import { SyntaxTreeDataProvider } from './SyntaxTreeDataProvider';
import { SyntaxExplorerView } from './SyntaxExplorerView';
import { PapyrusConfig } from './PapyrusConfig';

export function gameTypeToVariantName(gameType: PapyrusConfig.Type) {
    switch (gameType) {
        case PapyrusConfig.Type.Fallout4:
            return 'Fallout4';
        case PapyrusConfig.Type.Skyrim:
        case PapyrusConfig.Type.SkyrimSpecialEdition:
            return 'Skyrim';
    }
}

export class PapyrusServer {
    private readonly _extension: PapyrusExtension;
    private readonly _context: ExtensionContext;
    private _clientServer: ClientServer;
    private readonly _overriddenOrInactiveDecoration: TextEditorDecorationType;
    private readonly _disposables: Disposable[] = [];
    private readonly _gameType: PapyrusConfig.Type;
    private _isDisposed = false;

    private getToolPath() {
        const languageVariantName = gameTypeToVariantName(this._gameType);
        return this._context.asAbsolutePath(`./bin/${languageVariantName}/net461/DarkId.Papyrus.Host.${languageVariantName}/DarkId.Papyrus.Host.${languageVariantName}.exe`);
    }

    constructor(extension: PapyrusExtension, context: ExtensionContext, gameType: PapyrusConfig.Type) {
        this._extension = extension;
        this._context = context;
        this._gameType = gameType;

        this._overriddenOrInactiveDecoration = window.createTextEditorDecorationType({
            opacity: '0.5',
        });
    }

    get gameType() {
        return this._gameType;
    }

    async start() {
        const toolPath = this.getToolPath();

        const compilerAssemblyPath =
            process.platform === 'win32' ? this._extension.Config.GetCompilerPath : this._context.asAbsolutePath('../../dependencies/compilers/');
    
        this._clientServer = new ClientServer(toolPath, compilerAssemblyPath);
        if (workspace.name !== undefined) {
            await this._clientServer.start();
    
            if (this._isDisposed) {
                return;
            }

            if (process.env['PAPYRUS_EXTENSION_DEBUG']) {
                const syntaxTreeDataProvider = new SyntaxTreeDataProvider(this._clientServer);
                const syntaxExplorer = new SyntaxExplorerView('papyrus-lang-vscode.astTreeView', syntaxTreeDataProvider);
                syntaxExplorer.register();
            }
        }

        this._disposables.push(languages.registerCodeLensProvider(
            { language: 'papyrus', scheme: 'file' },
            new ScriptStatusCodeLensProvider(this)
        ));
    
        this._disposables.push(window.onDidChangeActiveTextEditor(this.updateTextEditorDecorations));
        this.updateTextEditorDecorations(window.activeTextEditor);
    }

    private updateTextEditorDecorations = async (editor: TextEditor) => {
        if (editor.document.languageId !== 'papyrus') {
            return;
        }
    
        try {
            const { documentIsUnresolved, documentIsOverridden } = await this.getDocumentScriptStatus(editor.document);
    
            if (documentIsUnresolved || documentIsOverridden) {
                editor.setDecorations(this._overriddenOrInactiveDecoration, [
                    {
                        range: new Range(
                            new Position(0, 0),
                            editor.document.positionAt(editor.document.getText().length)
                        ),
                    },
                ]);
            } else {
                editor.setDecorations(this._overriddenOrInactiveDecoration, []);
            }
        } catch {
            editor.setDecorations(this._overriddenOrInactiveDecoration, []);
        }
    }

    async getDocumentScriptStatus(document: TextDocument) {
        const scriptInfo = await this._clientServer.requestScriptInfo(document.uri.toString());
    
        const documentIsUnresolved = scriptInfo.identifiers.length === 0;
        const documentIsOverridden =
            !documentIsUnresolved &&
            !scriptInfo.identifierFiles.some((identifierFile) =>
                identifierFile.files.some((file) => file.toLowerCase() === document.uri.fsPath.toLowerCase())
            );
    
        return {
            documentIsUnresolved,
            documentIsOverridden,
            scriptInfo,
        };
    }

    get clientServer() {
        return this._clientServer;
    }

    dispose() {
        this._isDisposed = true;
        this._clientServer.stop();

        for (const disposable of this._disposables) {
            disposable.dispose();
        }
    }
}