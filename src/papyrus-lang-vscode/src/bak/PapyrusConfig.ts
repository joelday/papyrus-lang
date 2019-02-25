// import * as vscode from 'vscode';
// import * as path from 'path';
// import * as ini from 'ini';
// import * as fs from 'fs';

// export class PapyrusConfigManager {
//     private Config: Array<PapyrusConfig>;
//     public GameID: PapyrusConfig.Type;

//     constructor() {
//         this.Config = new Array<PapyrusConfig>();
//         this.Config.push(new PapyrusConfig(PapyrusConfig.Type.Fallout4, 'Fallout 4'));
//         this.Config.push(new PapyrusConfig(PapyrusConfig.Type.Skyrim, 'Skyrim LE'));
//         this.Config.push(new PapyrusConfig(PapyrusConfig.Type.SkyrimSpecialEdition, 'Skyrim SE'));
//         this.GameID = this.Config[0].GetCurrentGame;
//     }

//     public get Get(): PapyrusConfig {
//         return this.Config[this.GameID];
//     }

//     public get GetCurrentGame(): PapyrusConfig.Type {
//         return this.Config[this.GameID].GetCurrentGame;
//     }

//     public get GameName(): string {
//         return this.Config[this.GameID].GameName;
//     }

//     public get GetRootExecutablePath(): string {
//         return this.Config[this.GameID].GetRootExecutablePath;
//     }

//     public get GetRootPath(): string {
//         return this.Config[this.GameID].GetRootPath;
//     }

//     public get GetCompilerExecutablePath(): string {
//         return this.Config[this.GameID].GetCompilerExecutablePath;
//     }

//     public get GetCompilerPath(): string {
//         return this.Config[this.GameID].GetCompilerPath;
//     }

//     public get GetCompilerImports(): Array<string> {
//         return this.Config[this.GameID].GetCompilerImports;
//     }

//     public get GetCompilerMode(): string {
//         return this.Config[this.GameID].GetCompilerMode;
//     }

//     public get GetAssemblyFlag(): string {
//         return this.Config[this.GameID].GetAssemblyFlag;
//     }

//     public get GetFlagFileName(): string {
//         return this.Config[this.GameID].GetFlagFileName;
//     }

//     public get GetOutputPath(): string {
//         return this.Config[this.GameID].GetOutputPath;
//     }

//     public get IsCompilerPathValid(): boolean {
//         return this.Config[this.GameID].IsCompilerPathValid;
//     }

//     public get IsOutputPathValid(): boolean {
//         return this.Config[this.GameID].IsOutputPathValid;
//     }

//     public get IsRootPathValid(): boolean {
//         return this.Config[this.GameID].IsRootPathValid;
//     }

//     public SetAssemblyFlag(asmType: string) {
//         this.Config[this.GameID].SetAssemblyFlag(asmType);
//     }

//     public SetCompilerMode(compilerMode: string) {
//         this.Config[this.GameID].SetCompilerMode(compilerMode);
//     }

//     public SetCurrentGame(GameID: PapyrusConfig.Type) {
//         this.GameID = GameID; this.Config[this.GameID].SetCurrentGame();
//     }

//     public UpdateCurrentGame() {
//         this.SetCurrentGame(this.GetCurrentGame);
//     }
// }

// export class PapyrusConfig {
//     public GameID: PapyrusConfig.Type;
//     public GameName: string;

//     constructor(GameID: PapyrusConfig.Type, GameName: string) {
//         this.GameID = GameID;
//         this.GameName = GameName;
//     }

//     private GetConfigValueString(Section: string, Key: string, Fallback: string): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 let fileArray = this.GetWorkspaceConfiguration.get<Array<string>>('creationKitIniFiles').slice(0);

//                 for (let element of fileArray.reverse()) {
//                     let configPath = path.join(this.GetRootPath, element);
//                     if (fs.existsSync(configPath)) {
//                         let Config = ini.parse(fs.readFileSync(configPath, 'utf-8'));
//                         let Result = Config[Section][Key];

//                         if (Result !== undefined) {
//                             return Result.replace(new RegExp('"', 'g'), '');
//                         }
//                     }
//                 }

//                 return Fallback;
//         }
//     }

//     private get GetBaseConfiguration(): vscode.WorkspaceConfiguration {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return vscode.workspace.getConfiguration('papyrus');
//         }
//     }

//     private get GetWorkspaceConfiguration(): vscode.WorkspaceConfiguration {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//                 return vscode.workspace.getConfiguration('papyrus.fallout4');
//             case PapyrusConfig.Type.Skyrim:
//                 return vscode.workspace.getConfiguration('papyrus.skyrim');
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return vscode.workspace.getConfiguration('papyrus.skyrimSpecialEdition');
//         }
//     }

//     public get GetCurrentGame(): PapyrusConfig.Type {
//         switch (this.GetBaseConfiguration.get<string>('currentGame')) {
//             case 'Fallout 4':
//                 return PapyrusConfig.Type.Fallout4;
//             case 'Skyrim LE':
//                 return PapyrusConfig.Type.Skyrim;
//             case 'Skyrim SE':
//                 return PapyrusConfig.Type.SkyrimSpecialEdition;
//         }
//     }

//     public get GetRootExecutablePath(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//                 return path.join(this.GetRootPath, 'Fallout4.exe');
//             case PapyrusConfig.Type.Skyrim:
//                 return path.join(this.GetRootPath, 'TESV.exe');
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return path.join(this.GetRootPath, 'SkyrimSE.exe');
//         }
//     }

//     public get GetRootPath(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return this.GetWorkspaceConfiguration.get<string>('installPath');
//         }
//     }

//     public get GetCompilerExecutablePath(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return path.join(this.GetCompilerPath, 'PapyrusCompiler.exe');
//         }
//     }

//     public get GetCompilerPath(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return path.resolve(this.GetRootPath, this.GetConfigValueString('Papyrus', 'sCompilerPath', 'Papyrus Compiler\\'));
//         }
//     }

//     public get GetCompilerImports(): Array<string> {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//                 let sourceFolder = this.GetConfigValueString('Papyrus', 'sScriptSourceFolder', '.\\Data\\Scripts\\Source\\User\\');
//                 let importString = this.GetConfigValueString('Papyrus', 'sAdditionalImports', '$(source);.\\Data\\Scripts\\Source\\Base\\');

//                 let importArray = importString.split(';');
//                 let index = importArray.findIndex(value => (value === '$(source)'));

//                 if (index > -1) {
//                     importArray[index] = sourceFolder;
//                 }

//                 return importArray;

//             case PapyrusConfig.Type.Skyrim:
//                 return new Array<string>(this.GetConfigValueString('Papyrus', 'sScriptSourceFolder', '.\\Data\\Scripts\\Source\\'));

//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return new Array<string>(this.GetConfigValueString('Papyrus', 'sScriptSourceFolder', '.\\Data\\Source\\Scripts\\'));
//         }
//     }

//     public get GetCompilerMode(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return this.GetWorkspaceConfiguration.get<string>('compiler.mode');
//         }
//     }

//     public get GetAssemblyFlag(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return this.GetWorkspaceConfiguration.get<string>('compiler.asm');
//         }
//     }

//     public get GetFlagFileName(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//                 return 'Institute_Papyrus_Flags.flg';
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return 'TESV_Papyrus_Flags.flg';
//         }
//     }

//     public get GetOutputPath(): string {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 return path.resolve(this.GetRootPath, this.GetConfigValueString('Papyrus', 'sScriptCompiledFolder', '.\\Data\\Scripts\\'));
//         }
//     }

//     public get IsCompilerPathValid(): boolean {
//         if (fs.existsSync(this.GetCompilerPath)) {
//             if (fs.existsSync(this.GetCompilerExecutablePath)) {
//                 return true;
//             }

//             console.log('The Compiler Path exists, but is not correct');
//             return false;
//         }

//         console.log('Invalid Compiler Path');
//         return false;
//     }

//     public get IsOutputPathValid(): boolean {
//         if (fs.existsSync(this.GetOutputPath)) {
//             return true;
//         }

//         console.log('Invalid Output Path');
//         return false;
//     }

//     public get IsRootPathValid(): boolean {
//         if (fs.existsSync(this.GetRootPath)) {
//             if (fs.existsSync(this.GetRootExecutablePath)) {
//                 return true;
//             }

//             console.log('The Root Path exists, but is not correct');
//             return false;
//         }

//         console.log('Invalid Root Path');
//         return false;
//     }

//     public SetAssemblyFlag(asmType: string) {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 this.GetWorkspaceConfiguration.update('compiler.asm', asmType, (vscode.workspace.workspaceFolders === undefined));
//         }
//     }

//     public SetCompilerMode(compilerMode: string) {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 this.GetWorkspaceConfiguration.update('compiler.mode', compilerMode, (vscode.workspace.workspaceFolders === undefined));
//         }
//     }

//     public SetCurrentGame() {
//         switch (this.GameID) {
//             case PapyrusConfig.Type.Fallout4:
//             case PapyrusConfig.Type.Skyrim:
//             case PapyrusConfig.Type.SkyrimSpecialEdition:
//                 this.GetBaseConfiguration.update('currentGame', this.GameName, (vscode.workspace.workspaceFolders === undefined));
//         }
//     }
// }

// export module PapyrusConfig {
//     export enum Type {
//         None = -1,
//         Fallout4,
//         Skyrim,
//         SkyrimSpecialEdition
//     }
// }
