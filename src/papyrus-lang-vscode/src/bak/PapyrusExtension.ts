// import * as vscode from 'vscode';
// import * as fs from 'fs';

// import { PapyrusCompiler } from './PapyrusCompiler';
// import { PapyrusConfig, PapyrusConfigManager } from './PapyrusConfig';
// import { PapyrusServer } from './PapyrusServer';

// var Channel: vscode.OutputChannel;
// var extension: PapyrusExtension;

// export async function activate(context: vscode.ExtensionContext) {
//     extension = new PapyrusExtension(context);
// }

// export function deactivate() {
//     return extension.deactivate();
// }

// export class PapyrusExtension {
//     private readonly _context: vscode.ExtensionContext;

//     private _server: PapyrusServer;

//     private AssemblyStatusBarButton: vscode.StatusBarItem;
//     private CompilerStatusBarButton: vscode.StatusBarItem;
//     private LanguageStatusBarButton: vscode.StatusBarItem;

//     public Config: PapyrusConfigManager;

//     constructor(context: vscode.ExtensionContext) {
//         this._context = context;

//         this._context.subscriptions.push(vscode.languages.setLanguageConfiguration('papyrus', {
//             comments: {
//                 lineComment: ';',
//                 blockComment: [';/', '/;'],
//             },
//             brackets: [['{', '}'], ['[', ']'], ['(', ')']],
//             indentationRules: {
//                 increaseIndentPattern: /^\s*(if|(\S+\s+)?(property\W+\w+(?!.*(auto)))|struct|group|state|event|(\S+\s+)?(function.*\(.*\)(?!.*native))|else|elseif)/i,
//                 decreaseIndentPattern: /^\s*(endif|endproperty|endstruct|endgroup|endstate|endevent|endfunction|else|elseif)/i,
//             },
//         }));

//         this.Config = new PapyrusConfigManager();

//         this.RegisterCommand('papyrus.compile', () => { this.CompileInPlace(); });
//         this.RegisterCommand('papyrus.compiledebug', () => { this.CompileActiveFile(false, false, false); });
//         this.RegisterCommand('papyrus.compilerelease', () => { this.CompileActiveFile(true, true, false); });
//         this.RegisterCommand('papyrus.compilefinal', () => { this.CompileActiveFile(true, true, true); });

//         this.RegisterCommand('papyrus.createproject', () => { this.CreateProjectFile(); });

//         this.RegisterCommand('papyrus.setassemblymode', () => { this.SelectAssemblyMode(); });
//         this.RegisterCommand('papyrus.setcompilermode', () => { this.SelectCompilerMode(); });
//         this.RegisterCommand('papyrus.setlanguagemode', () => { this.SelectLanguageMode(); });

//         this.AssemblyStatusBarButton = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 98);
//         this.AssemblyStatusBarButton.command = 'papyrus.setassemblymode';
//         this._context.subscriptions.push(this.AssemblyStatusBarButton);

//         this.CompilerStatusBarButton = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 99);
//         this.CompilerStatusBarButton.command = 'papyrus.setcompilermode';
//         this._context.subscriptions.push(this.CompilerStatusBarButton);

//         this.LanguageStatusBarButton = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
//         this.LanguageStatusBarButton.command = 'papyrus.setlanguagemode';
//         this._context.subscriptions.push(this.LanguageStatusBarButton);

//         this._context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor((e) => { this.UpdateStatusBarButtons(); }));

//         this._context.subscriptions.push(vscode.workspace.onDidChangeConfiguration((e) => {
//             if (e.affectsConfiguration('papyrus')) {
//                 this.Config.UpdateCurrentGame();
//                 this.UpdateStatusBarButtons();
//                 this.startOrResetServer();
//             }
//         }));

//         this.UpdateStatusBarButtons();
//         this.startOrResetServer();
//     }

//     private get IsPapyrusActive(): boolean {
//         return (vscode.window.activeTextEditor !== undefined) ?
//             vscode.window.activeTextEditor.document.languageId.includes('papyrus') : false;
//     }

//     private BuildCompiler(Config: PapyrusConfig, Folder: string, DoOptimize: boolean, IsRelease: boolean, IsFinal: boolean, Scripts: Array<string>): PapyrusCompiler {
//         if (Channel === undefined) {
//             Channel = vscode.window.createOutputChannel('Papyrus Compiler');
//         }

//         Channel.clear(); Channel.show();
//         return new PapyrusCompiler(Config, Folder, DoOptimize, IsRelease, IsFinal, Scripts, Channel);
//     }

//     private CompileInPlace() {
//         if (this.IsPapyrusActive) {
//             vscode.window.activeTextEditor.document.save();

//             let Compiler = this.BuildCompiler(this.Config.Get, this._context.storagePath, false, false, false,
//                 new Array<string>(vscode.window.activeTextEditor.document.fileName));

//             switch (this.Config.GetCompilerMode) {
//                 case 'Release Final':
//                     Compiler.IsFinal = true;
//                 case 'Release':
//                     Compiler.IsRelease = true;
//                     Compiler.DoOptimize = true;
//                 case 'Debug':
//                     Compiler.Run();
//             }
//         }
//     }

//     private CompileActiveFile(Optimize: boolean, Release: boolean, Final: boolean) {
//         if (this.IsPapyrusActive) {
//             vscode.window.activeTextEditor.document.save();

//             let Compiler = this.BuildCompiler(this.Config.Get, this._context.storagePath, Optimize, Release, Final,
//                 new Array<string>(vscode.window.activeTextEditor.document.fileName));

//             Compiler.Run();
//         }
//     }

//     private CreateProjectFile() {
//         vscode.window.showOpenDialog({
//             canSelectFiles: true,
//             canSelectMany: true,
//             defaultUri: undefined,
//             filters: {
//                 'Papyrus Source': ['psc']
//             }
//         }).then(files => {
//             if (files) {
//                 let FilePaths = new Array<string>();
//                 for (let file of files) {
//                     FilePaths.push(file.fsPath);
//                 }

//                 switch (this.Config.GameID) {
//                     case PapyrusConfig.Type.Fallout4:
//                         vscode.window.showSaveDialog({
//                             defaultUri: undefined,
//                             filters: {
//                                 'Papyrus Project': ['ppj']
//                             }
//                         }).then(file => {
//                             let Compiler = this.BuildCompiler(this.Config.Get, this._context.storagePath,
//                                 false, false, false, FilePaths);

//                             if (file) {
//                                 fs.writeFileSync(file.fsPath, Compiler.ProjectXML());
//                                 vscode.window.showTextDocument(file);
//                             }
//                         });
//                         break;
//                     case PapyrusConfig.Type.Skyrim:
//                     case PapyrusConfig.Type.SkyrimSpecialEdition:
//                         return;
//                 }
//             }
//         });
//     }

//     private RegisterCommand(commandName: string, callback: () => any): void {
//         this._context.subscriptions.push(vscode.commands.registerTextEditorCommand(commandName, callback));
//     }

//     private async SelectAssemblyMode() {
//         const value = await vscode.window.showQuickPick(
//             [
//                 { label: 'None', description: 'Does not generate an assembly file and does not run the assembler.' },
//                 { label: 'Keep', description: 'Keeps the assembly file after running the assembler.' },
//                 { label: 'Only', description: 'Generates an assembly file but does not run the assembler.' },
//                 { label: 'Discard', description: '(Default) Does not keep the assembly file after running the assembler.' }
//             ],
//             { placeHolder: 'Select the Assembly flag to compile with.', canPickMany: false });

//         if (value) {
//             this.Config.SetAssemblyFlag(value.label);
//             this.UpdateStatusBarButton(this.AssemblyStatusBarButton, PapyrusConfig.Type.None, `Asm: ${value.label}`);
//         }
//     }

//     private async SelectCompilerMode() {
//         const value = await vscode.window.showQuickPick(
//             [
//                 { label: 'Debug', description: '(Default) Optimization disabled, BetaOnly and DebugOnly functions enabled.' },
//                 { label: 'Release', description: 'Optimization and BetaOnly functions enabled, DebugOnly Functions disabled.' },
//                 { label: 'Release Final', description: 'Optimization enabled, BetaOnly and DebugOnly functions disabled.' }
//             ],
//             { placeHolder: 'Select the flags to compile with.', canPickMany: false });

//         if (value) {
//             this.Config.SetCompilerMode(value.label);
//             this.UpdateStatusBarButton(this.CompilerStatusBarButton, PapyrusConfig.Type.Fallout4, value.label);
//         }
//     }

//     private async SelectLanguageMode() {
//         const value = await vscode.window.showQuickPick(
//             [
//                 { label: 'Fallout 4', type: PapyrusConfig.Type.Fallout4 },
//                 { label: 'Skyrim', type: PapyrusConfig.Type.Skyrim },
//                 { label: 'Skyrim Special Edition', type: PapyrusConfig.Type.SkyrimSpecialEdition }
//             ],
//             { placeHolder: 'Select the version of Papyrus to use.', canPickMany: false });

//         if (value) {
//             this.Config.SetCurrentGame(value.type);
//             this.UpdateStatusBarButtons();
//             this.startOrResetServer();
//         }
//     }

//     private startOrResetServer() {
//         if (!this._server || this._server.gameType !== this.Config.GameID) {
//             if (this._server) {
//                 this._server.dispose();
//             }

//             this._server = new PapyrusServer(this, this._context, this.Config.GameID);
//             this._server.start();
//         }
//     }

//     private UpdateStatusBarButton(button: vscode.StatusBarItem, ReqGameID: PapyrusConfig.Type, label1: string) {
//         if (this.IsPapyrusActive) {
//             button.text = label1;
//             if ((ReqGameID === this.Config.GameID) || (ReqGameID === PapyrusConfig.Type.None)) {
//                 button.show();
//             } else {
//                 button.hide();
//             }
//         } else {
//             button.hide();
//         }
//     }

//     private UpdateStatusBarButtons() {
//         this.UpdateStatusBarButton(this.AssemblyStatusBarButton, PapyrusConfig.Type.None, `Asm: ${this.Config.GetAssemblyFlag}`);
//         this.UpdateStatusBarButton(this.LanguageStatusBarButton, PapyrusConfig.Type.None, this.Config.GameName);
//         this.UpdateStatusBarButton(this.CompilerStatusBarButton, PapyrusConfig.Type.Fallout4, this.Config.GetCompilerMode);
//     }

//     deactivate() {
//         if (this._server) {
//             this._server.dispose();
//         }

//         for (const disposable of this._context.subscriptions) {
//             disposable.dispose();
//         }
//     }
// }
