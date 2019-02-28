// import { FileSystemWatcher, workspace, CancellationToken, ProviderResult, Disposable } from 'vscode';
// import {
//     LanguageClient,
//     LanguageClientOptions,
//     ServerOptions,
//     Trace,
//     TextDocument,
//     RequestType,
//     TextDocumentIdentifier,
//     TextDocumentRegistrationOptions,
//     Range
// } from 'vscode-languageclient';

// import psList from 'ps-list';
// import { exec } from 'child_process';

// export class ClientServer {
//     private readonly _serverOptions: ServerOptions;
//     private _client: LanguageClient;
//     private _fsWatcher: FileSystemWatcher;
//     private _clientDisposable: Disposable;

//     constructor(toolPath: string, compilerAssemblyPath: string) {
//         const serverExecution = getServerExecution(toolPath, compilerAssemblyPath);

//         this._serverOptions = {
//             run: serverExecution,
//             debug: serverExecution,
//         };
//     }

//     /// examples
//     // let Result = await clientServer.requestRegistryInstallPath('Fallout4');
//     // let Result = await clientServer.requestRegistryInstallPath('Skyrim');
//     // let Result = await clientServer.requestRegistryInstallPath('Skyrim Special Edition');
//     public requestRegistryInstallPath(KeyName: string): Thenable<RegistryInstallPathInfo> {
//         return this._client.sendRequest(documentRegistryInstallPathRequestType.type, {
//             RegKeyName: KeyName,
//         });
//     }

//     public requestSyntaxTree(uri: string): Thenable<DocumentSyntaxTree> {
//         return this._client.sendRequest(documentSyntaxTreeRequestType.type, {
//             textDocument: TextDocumentIdentifier.create(uri),
//         });
//     }

//     public requestScriptInfo(uri: string): Thenable<DocumentScriptInfo> {
//         return this._client.sendRequest(documentScriptInfoRequestType.type, {
//             textDocument: TextDocumentIdentifier.create(uri),
//         });
//     }

//     public isActive(): boolean {
//         if (this._client) {
//             return true;
//         }
//         return false;
//     }

//     public async start() {
//         if (this._client) {
//             return;
//         }

//         this._fsWatcher = workspace.createFileSystemWatcher('**/*.{flg,ppj,psc}');

//         const clientOptions: LanguageClientOptions = {
//             documentSelector: [
//                 { scheme: 'file', language: 'papyrus' },
//                 { scheme: 'file', language: 'papyrus-project' },
//             ],
//             synchronize: {
//                 fileEvents: this._fsWatcher,
//                 configurationSection: 'papyrus',
//             },
//         };

//         // In order to find the new process id, we need to exclude any current running instances of the language server.
//         let existingProcessIds: number[];
//         if (process.platform === 'win32' && process.env['PAPYRUS_EXTENSION_DEBUG']) {
//             const processes = await psList();
//             existingProcessIds = processes.filter((p) => p.name.startsWith('DarkId.Papyrus.Host')).map((p) => p.pid);
//         }

//         // Create the language client and start the client.
//         this._client = new LanguageClient('papyrus', 'Papyrus Language Service', this._serverOptions, clientOptions);

//         this._client.trace = Trace.Verbose;
//         this._clientDisposable = this._client.start();

//         await this._client.onReady();

//         if (!this._clientDisposable) {
//             return;
//         }

//         if (process.platform === 'win32' && process.env['PAPYRUS_EXTENSION_DEBUG']) {
//             const processes = await psList();
//             const serverProcessId = processes.find(
//                 (p) => p.name.startsWith('DarkId.Papyrus.Host') && existingProcessIds.indexOf(p.pid) === -1
//             )!.pid;

//             if (serverProcessId) {
//                 exec(`vsjitdebugger.exe -p ${serverProcessId}`);
//             }
//         }
//     }

//     public async stop() {
//         if (!this._client || !this._clientDisposable) {
//             return;
//         }

//         try {
//             this._clientDisposable.dispose();
//             this._clientDisposable = null;

//             this._fsWatcher.dispose();
//             await this._client.stop();
//         } finally {
//             this._fsWatcher = null;
//             this._client = null;
//         }
//     }
// }
