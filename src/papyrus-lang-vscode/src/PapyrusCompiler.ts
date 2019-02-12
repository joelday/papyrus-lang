// import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';

import { spawn } from 'child_process';

import { CompilerChannel } from './extension';
import { PapyrusConfig } from './PapyrusConfig';

export class PapyrusCompiler {
    private RootPath: string;
    private OutputPath: string;
    private CompilerPath: string;
    private FlagsFileName: string;
    private AssemblySetting: string;
    private IsProject: boolean;
    private ImportList: Array<string>;
    private ScriptList: Array<string>;
    private FolderList: Array<string>;
    private Config: PapyrusConfig;

    public IsRelease: boolean;
    public IsFinal: boolean;
    public DoOptimize: boolean;

    constructor(Config: PapyrusConfig, DoOptimize: boolean, IsRelease: boolean, IsFinal: boolean, Scripts: Array<string>) {
        this.Config = Config;
        this.RootPath = this.Config.GetRootPath;
        this.OutputPath = this.Config.GetOutputPath;
        this.CompilerPath = this.Config.GetCompilerExecutablePath;

        this.FlagsFileName = this.Config.GetFlagFileName;
        this.AssemblySetting = this.Config.GetAssemblyFlag;

        this.IsProject = false;
        this.IsRelease = IsRelease;
        this.IsFinal = IsFinal;

        this.DoOptimize = DoOptimize;

        this.ImportList = this.Config.GetCompilerImports.slice(0);
        this.ScriptList = Scripts.slice(0);
        this.FolderList = new Array<string>();

        CompilerChannel.show();
    }

    private GetCompileArgs(): Array<string> {
        let Result = new Array<string>();
        switch (this.Config.GameID) {
            case PapyrusConfig.Type.Fallout4:
                if (this.IsProject) {
                    Result.push(this.ScriptList[0]);
                } else {
                    let tempDir = path.join(os.tmpdir(), 'papyrus-lang');
                    if (!fs.existsSync(tempDir)) {
                        fs.mkdirSync(tempDir);
                    }

                    let filePath = path.join(tempDir, '\\PapyrusLangTemp.ppj');
                    fs.writeFileSync(filePath, this.ProjectXML());
                    Result.push(filePath);
                }
                break;

            case PapyrusConfig.Type.Skyrim:
            case PapyrusConfig.Type.SkyrimSpecialEdition:
                if (this.FolderList.length > 0) {
                    Result.push(this.FolderList[0]);
                    Result.push('-all');
                } else {
                    Result.push(this.ScriptList.join(';'));
                }

                Result.push('-f=' + this.FlagsFileName);
                Result.push('-i=' + this.ImportList.join(';'));
                Result.push('-o=' + this.OutputPath);

                switch (this.AssemblySetting.toLowerCase()) {
                    case 'none':
                        Result.push('-noasm'); break;
                    case 'keep':
                        Result.push('-keepasm'); break;
                    case 'only':
                        Result.push('-asmonly'); break;
                    default:
                        break;
                }

                if (this.DoOptimize) {
                    Result.push('-optimize');
                }
        }

        return Result;
    }

    private IsPathNamespace(filePath: string): boolean {
        switch (this.Config.GameID) {
            case PapyrusConfig.Type.Fallout4:
                filePath = filePath.substring(path.dirname(filePath).length);
                return (filePath.split('\\').length > 3);

            // Skyrim doesn't support namespaces
            case PapyrusConfig.Type.Skyrim:
            case PapyrusConfig.Type.SkyrimSpecialEdition:
                return false;
        }
    }

    private IsPathNotNamespace(filePath: string): boolean {
        return (!this.IsPathNamespace(filePath));
    }

    private IsStringInArray(searchArray: Array<string>, searchString: string): boolean {
        return (searchArray.findIndex(foundString => (path.normalize(foundString) === path.normalize(searchString))) > -1);
    }

    private IsStringNotInArray(searchArray: Array<string>, searchString: string): boolean {
        return (!this.IsStringInArray(searchArray, searchString));
    }

    private get AreImportsValid(): boolean {
        if (this.ImportList.length > 0) {
            return true;
        }

        console.log('Invalid Imports');
        return false;
    }

    private get AreScriptsValid(): boolean {
        if ((this.ScriptList.length > 0) || (this.FolderList.length > 0)) {
            return true;
        }

        console.log('Invalid Scripts');
        return false;
    }

    private get IsAssemblySettingValid(): boolean {
        switch (this.AssemblySetting.toLowerCase()) {
            case 'none':
            case 'keep':
            case 'only':
            case 'discard':
                return true;
        }

        console.log('Invalid Assembly Setting');
        return false;
    }

    private SortImports(): Array<string> {
        let FinalList = new Array<string>();

        this.ImportList.forEach((importPath) => {
            importPath = path.normalize(path.resolve(this.RootPath, importPath));

            if (fs.existsSync(importPath)) {
                if (fs.lstatSync(importPath).isDirectory()) {
                    if (this.IsStringNotInArray(FinalList, importPath)) {
                        if (this.IsPathNotNamespace(importPath)) {
                            FinalList.push(importPath);
                        }
                    }
                }
            }
        });

        return FinalList;
    }

    private SortScripts(): Array<string> {
        let FinalList = new Array<string>();
        if (this.ScriptList.length === 0) {
            return FinalList;
        }

        this.ScriptList.forEach((scriptFile) => {
            let scriptPath = path.resolve(this.RootPath, scriptFile);
            let scriptExt = path.extname(scriptFile);

            switch (scriptExt) {
                case '.psc':
                    this.ImportList.push(path.dirname(scriptPath) + '\\');
                    FinalList.push(scriptPath);
                    break;

                case '.ppj':
                    this.IsProject = true;
                    if (this.ScriptList.length > 1) {
                        console.log('Script List is invalid, projects can only be compiled by themselves');
                        return new Array<string>();
                    }
                    break;

                default:
                    console.log(`Error, unrecognized file type: ${scriptExt}`);
            }
        });

        return FinalList;
    }

    public ProjectXML(): string {
        var XMLWriter = require('xml-writer');
        let ProjectFile = new XMLWriter(true);

        ProjectFile.startDocument();
        ProjectFile.startElement('PapyrusProject');
        ProjectFile.writeAttribute('xmlns', 'PapyrusProject.xsd');
        ProjectFile.writeAttribute('Output', this.OutputPath);
        ProjectFile.writeAttribute('Flags', this.FlagsFileName);
        ProjectFile.writeAttribute('Asm', this.AssemblySetting);
        ProjectFile.writeAttribute('Optimize', this.DoOptimize.toString());
        ProjectFile.writeAttribute('Release', this.IsRelease.toString());
        ProjectFile.writeAttribute('Final', this.IsFinal.toString());

        ProjectFile.startElement('Imports');
        for (let element of this.ImportList) {
            ProjectFile.startElement('Import');
            ProjectFile.text(element);
            ProjectFile.endElement();
        } ProjectFile.endElement();

        if (this.FolderList.length > 0) {
            ProjectFile.startElement('Folders');
            for (let element of this.FolderList) {
                ProjectFile.startElement('Folder');
                ProjectFile.text(element);
                ProjectFile.endElement();
            } ProjectFile.endElement();
        }

        if (this.ScriptList.length > 0) {
            ProjectFile.startElement('Scripts');
            for (let element of this.ScriptList) {
                ProjectFile.startElement('Script');
                ProjectFile.text(element);
                ProjectFile.endElement();
            } ProjectFile.endElement();
        }

        ProjectFile.endDocument();
        return ProjectFile.toString();
    }

    public Run() {
        let RootPathValid = this.Config.IsRootPathValid;
        let OutputPathValid = this.Config.IsOutputPathValid;
        let CompilerPathValid = this.Config.IsCompilerPathValid;

        if (RootPathValid && OutputPathValid && CompilerPathValid) {
            this.ScriptList = this.SortScripts();
            this.ImportList = this.SortImports();

            let ImportsValid = this.AreImportsValid;
            let ScriptsValid = this.AreScriptsValid;
            let AsmSettValid = this.IsAssemblySettingValid;

            if (ImportsValid && ScriptsValid && AsmSettValid) {
                var Compiler = spawn(this.CompilerPath, this.GetCompileArgs());
                Compiler.stdout.on('data', function (data) {
                    CompilerChannel.append(data.toString());
                });

                Compiler.stderr.on('data', function (data) {
                    CompilerChannel.append(data.toString());
                });

                return;
            }
        }

        console.log('Errors happened');
    }
}
