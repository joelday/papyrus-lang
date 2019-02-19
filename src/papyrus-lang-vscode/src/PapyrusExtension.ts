import * as vscode from 'vscode';
import * as fs from 'fs';

import { PapyrusCompiler } from './PapyrusCompiler';
import { PapyrusConfig } from './PapyrusConfig';
import { PapyrusServer } from './PapyrusServer';

function RegisterCommand(context: vscode.ExtensionContext, commandName: string, callback: () => any): void {
    context.subscriptions.push(vscode.commands.registerTextEditorCommand(commandName, callback));
}

class PapyrusConfigManager {
    private Config: Array<PapyrusConfig>;
    public GameID: PapyrusConfig.Type;

    constructor() {
        this.Config = new Array<PapyrusConfig>();
        this.Config.push(new PapyrusConfig(PapyrusConfig.Type.Fallout4, 'Fallout 4'));
        this.Config.push(new PapyrusConfig(PapyrusConfig.Type.Skyrim, 'Skyrim LE'));
        this.Config.push(new PapyrusConfig(PapyrusConfig.Type.SkyrimSpecialEdition, 'Skyrim SE'));
        this.GameID = PapyrusConfig.Type.SkyrimSpecialEdition;
    }

    public get Get(): PapyrusConfig {
        return this.Config[this.GameID];
    }

    public get GameName(): string {
        return this.Config[this.GameID].GameName;
    }

    public get GetRootExecutablePath(): string {
        return this.Config[this.GameID].GetRootExecutablePath;
    }

    public get GetRootPath(): string {
        return this.Config[this.GameID].GetRootPath;
    }

    public get GetCompilerExecutablePath(): string {
        return this.Config[this.GameID].GetCompilerExecutablePath;
    }

    public get GetCompilerPath(): string {
        return this.Config[this.GameID].GetCompilerPath;
    }

    public get GetCompilerImports(): Array<string> {
        return this.Config[this.GameID].GetCompilerImports;
    }

    public get GetCompilerMode(): string {
        return this.Config[this.GameID].GetCompilerMode;
    }

    public get GetAssemblyFlag(): string {
        return this.Config[this.GameID].GetAssemblyFlag;
    }

    public get GetFlagFileName(): string {
        return this.Config[this.GameID].GetFlagFileName;
    }

    public get GetOutputPath(): string {
        return this.Config[this.GameID].GetOutputPath;
    }

    public get IsCompilerPathValid(): boolean {
        return this.Config[this.GameID].IsCompilerPathValid;
    }

    public get IsOutputPathValid(): boolean {
        return this.Config[this.GameID].IsOutputPathValid;
    }

    public get IsRootPathValid(): boolean {
        return this.Config[this.GameID].IsRootPathValid;
    }

    public SetAssemblyFlag(asmType: string) {
        this.Config[this.GameID].SetAssemblyFlag(asmType);
    }

    public SetCompilerMode(compilerMode: string) {
        this.Config[this.GameID].SetCompilerMode(compilerMode);
    }

    public UpdateCurrentGame(GameID: PapyrusConfig.Type) {
        this.GameID = GameID;
    }
}

export class PapyrusExtension {
    private readonly _context: vscode.ExtensionContext;
    
    private AssemblyStatusBarButton: vscode.StatusBarItem;
    private CompilerStatusBarButton: vscode.StatusBarItem;
    private LanguageStatusBarButton: vscode.StatusBarItem;
    private _server: PapyrusServer;

    public Config: PapyrusConfigManager;

    constructor(context: vscode.ExtensionContext) {
        this._context = context;
        
        this._context.subscriptions.push(vscode.languages.setLanguageConfiguration('papyrus', {
            comments: {
                lineComment: ';',
                blockComment: [';/', '/;'],
            },
            brackets: [['{', '}'], ['[', ']'], ['(', ')']],
            indentationRules: {
                increaseIndentPattern: /^\s*(if|(\S+\s+)?(property\W+\w+(?!.*(auto)))|struct|group|state|event|(\S+\s+)?(function.*\(.*\)(?!.*native))|else|elseif)/i,
                decreaseIndentPattern: /^\s*(endif|endproperty|endstruct|endgroup|endstate|endevent|endfunction|else|elseif)/i,
            },
        }));

        this.Config = new PapyrusConfigManager();

        RegisterCommand(context, 'papyrus.compile', () => { this.CompileInPlace(); });
        RegisterCommand(context, 'papyrus.compiledebug', () => { this.CompileActiveFile(false, false, false); });
        RegisterCommand(context, 'papyrus.compilerelease', () => { this.CompileActiveFile(true, true, false); });
        RegisterCommand(context, 'papyrus.compilefinal', () => { this.CompileActiveFile(true, true, true); });

        //RegisterCommand(context, 'papyrus.compilefile',     () => { this.CompileFile(); });
        //RegisterCommand(context, 'papyrus.compilefolder',   () => { this.CompileFolder(); });
        //RegisterCommand(context, 'papyrus.compiletarget',   () => { this.CompileTarget(); });

        RegisterCommand(context, 'papyrus.createproject', () => { this.CreateProjectFile(); });

        RegisterCommand(context, 'papyrus.setassemblymode', () => { this.SelectAssemblyMode(); });
        RegisterCommand(context, 'papyrus.setcompilermode', () => { this.SelectCompilerMode(); });
        RegisterCommand(context, 'papyrus.setlanguagemode', () => { this.SelectLanguageMode(); });

        this.AssemblyStatusBarButton = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 98);
        this.AssemblyStatusBarButton.command = 'papyrus.setassemblymode';
        context.subscriptions.push(this.AssemblyStatusBarButton);

        this.CompilerStatusBarButton = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 99);
        this.CompilerStatusBarButton.command = 'papyrus.setcompilermode';
        context.subscriptions.push(this.CompilerStatusBarButton);

        this.LanguageStatusBarButton = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
        this.LanguageStatusBarButton.command = 'papyrus.setlanguagemode';
        context.subscriptions.push(this.LanguageStatusBarButton);

        context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(this.UpdateStatusBarButtons));
        this.UpdateStatusBarButtons();

        this.startOrResetServer();
    }

    private startOrResetServer() {
        if (!this._server || this._server.gameType !== this.Config.GameID) {
            if (this._server) {
                this._server.dispose();
            }

            this._server = new PapyrusServer(this, this._context, this.Config.GameID);
            this._server.start();
        }
    }

    private get IsPapyrusActive(): boolean {
        if (vscode.window.activeTextEditor !== undefined) {
            return vscode.window.activeTextEditor.document.languageId.includes('papyrus');
        }
    }

    private CompileInPlace() {
        if (this.IsPapyrusActive) {
            vscode.window.activeTextEditor.document.save();
            let Compiler = new PapyrusCompiler(this.Config.Get, false, false, false,
                new Array<string>(vscode.window.activeTextEditor.document.fileName));

            switch (this.Config.GetCompilerMode) {
                case 'Release Final':
                    Compiler.IsFinal = true;
                case 'Release':
                    Compiler.IsRelease = true;
                    Compiler.DoOptimize = true;
                case 'Debug':
                    Compiler.Run();
            }
        }
    }

    private CompileActiveFile(Optimize: boolean, Release: boolean, Final: boolean) {
        if (this.IsPapyrusActive) {
            vscode.window.activeTextEditor.document.save();

            let Compiler = new PapyrusCompiler(this.Config.Get, Optimize, Release, Final,
                new Array<string>(vscode.window.activeTextEditor.document.fileName));

            Compiler.Run();
        }
    }

    private CreateProjectFile() {
        vscode.window.showOpenDialog({
            canSelectFiles: true,
            canSelectMany: true,
            defaultUri: undefined,
            filters: {
                'Papyrus Source': ['psc']
            }
        }).then(files => {
            if (files) {
                let FilePaths = new Array<string>();
                for (let file of files) {
                    FilePaths.push(file.fsPath);
                }

                switch (this.Config.GameID) {
                    case PapyrusConfig.Type.Fallout4:
                        vscode.window.showSaveDialog({
                            defaultUri: undefined,
                            filters: {
                                'Papyrus Project': ['ppj']
                            }
                        }).then(file => {
                            let Compiler = new PapyrusCompiler(this.Config.Get, false, false, false, FilePaths);

                            if (file) {
                                fs.writeFileSync(file.fsPath, Compiler.ProjectXML());
                                vscode.window.showTextDocument(file);
                            }
                        });
                        break;
                    case PapyrusConfig.Type.Skyrim:
                    case PapyrusConfig.Type.SkyrimSpecialEdition:
                        return;
                }
            }
        });
    }

    private async SelectAssemblyMode() {
        const value = await vscode.window.showQuickPick(
            [
                { label: 'None', description: 'Does not generate an assembly file and does not run the assembler.' },
                { label: 'Keep', description: 'Keeps the assembly file after running the assembler.' },
                { label: 'Only', description: 'Generates an assembly file but does not run the assembler.' },
                { label: 'Discard', description: '(Default) Does not keep the assembly file after running the assembler.' }
            ],
            { placeHolder: 'Select the Assembly flag to compile with.', canPickMany: false });

        if (value) {
            this.Config.SetAssemblyFlag(value.label);
            this.UpdateStatusBarButton(this.AssemblyStatusBarButton, -1, `Asm: ${this.Config.GetAssemblyFlag}`, value.label);
        }
    }

    private async SelectCompilerMode() {
        const value = await vscode.window.showQuickPick(
            [
                { label: 'Debug', description: '(Default) Optimization disabled, BetaOnly and DebugOnly functions enabled.' },
                { label: 'Release', description: 'Optimization and BetaOnly functions enabled, DebugOnly Functions disabled.' },
                { label: 'Release Final', description: 'Optimization enabled, BetaOnly and DebugOnly functions disabled.' }
            ],
            { placeHolder: 'Select the flags to compile with.', canPickMany: false });

        if (value) {
            this.Config.SetAssemblyFlag(value.label);
            this.UpdateStatusBarButton(this.CompilerStatusBarButton, PapyrusConfig.Type.Fallout4, this.Config.GetCompilerMode, value.label);
        }
    }

    private async SelectLanguageMode() {
        const value = await vscode.window.showQuickPick(
            [
                { label: 'Fallout 4', statusLabel: 'Fallout 4', type: PapyrusConfig.Type.Fallout4 },
                { label: 'Skyrim', statusLabel: 'Skyrim LE', type: PapyrusConfig.Type.Skyrim },
                { label: 'Skyrim Special Edition', statusLabel: 'Skyrim SE', type: PapyrusConfig.Type.SkyrimSpecialEdition }
            ],
            { placeHolder: 'Select the version of Papyrus to use.', canPickMany: false });

        if (value) {
            this.Config.UpdateCurrentGame(value.type);
            this.UpdateStatusBarButtons();
            this.startOrResetServer();
        }
    }

    private UpdateStatusBarButton(button: vscode.StatusBarItem, ReqGameID: PapyrusConfig.Type, label1: string, label2?: string) {
        if (this.IsPapyrusActive) {
            button.text = (label2 !== undefined) ? label2 : label1;
            if ((this.Config.GameID !== ReqGameID) && (ReqGameID > -1)) {
                button.hide();
            } else {
                button.show();
            }
        } else {
            button.hide();
        }
    }

    private UpdateStatusBarButtons() {
        this.UpdateStatusBarButton(this.AssemblyStatusBarButton, -1, `Asm: ${this.Config.GetAssemblyFlag}`);
        this.UpdateStatusBarButton(this.LanguageStatusBarButton, -1, this.Config.GameName);

        this.UpdateStatusBarButton(this.CompilerStatusBarButton, PapyrusConfig.Type.Fallout4, this.Config.GetCompilerMode);
    }

    deactivate() {
        if (this._server) {
            this._server.dispose();
        }

        for (const disposable of this._context.subscriptions) {
            disposable.dispose();
        }
    }
}
