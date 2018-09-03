import { IInstantiationService } from 'decoration-ioc';
import * as path from 'path';
import URI from 'vscode-uri';
import { DoublyLinkedMap } from '../common/DoublyLinkedMap';
import {
    IdentifierNode,
    isContainer,
    Node,
    NodeKind,
    ScriptNode,
    TypeIdentifierNode,
} from '../parser/Node';
import { Project } from '../projects/Project';
import { ProjectConfig } from '../projects/ProjectConfig';
import { IScriptTextProvider } from '../sources/ScriptTextProvider';
import { intrinsicTypes } from '../types/Type';
import { TypeChecker } from '../types/TypeChecker';
import { DisplayTextEmitter } from './DisplayTextEmitter';
import { ReferenceResolver } from './ReferenceResolver';
import { ScriptFile } from './ScriptFile';

export class Program {
    public get project() {
        return this._project;
    }

    public get scriptFiles(): ReadonlyMap<string, ScriptFile> {
        return this._scriptFiles;
    }

    public get scriptNames() {
        return this._scriptNames || [];
    }

    public get typeChecker() {
        return this._typeChecker;
    }

    public get referenceResolver() {
        return this._referenceResolver;
    }

    private readonly _project: Project;
    private readonly _scriptTextProvider: IScriptTextProvider;

    private readonly _scriptFiles: Map<string, ScriptFile> = new Map();

    private readonly _typeChecker: TypeChecker;
    private readonly _displayTextEmitter: DisplayTextEmitter;
    private readonly _referenceResolver: ReferenceResolver;

    private _fileList: DoublyLinkedMap<string, string>;
    private _scriptNames: string[];

    constructor(
        projectConfig: ProjectConfig,
        @IScriptTextProvider scriptTextProvider: IScriptTextProvider,
        @IInstantiationService instantiationService: IInstantiationService
    ) {
        this._project = instantiationService.createInstance(
            Project,
            projectConfig
        );
        this._scriptTextProvider = scriptTextProvider;
        this._typeChecker = new TypeChecker(this);
        this._displayTextEmitter = new DisplayTextEmitter(this);
        this._referenceResolver = new ReferenceResolver(this);

        this.refreshProjectFiles();
    }

    public refreshProjectFiles() {
        const scripts = this._project.resolveFiles();

        this._fileList = DoublyLinkedMap.fromMap(scripts, (key) =>
            key.toLowerCase()
        );

        this._scriptNames = Array.from(scripts.keys());
    }

    public getScriptFileByUri(
        uri: string,
        explicitOnly: boolean = false
    ): ScriptFile {
        if (!this._fileList.hasValue(uri)) {
            if (explicitOnly) {
                return null;
            }

            const filePath = URI.parse(uri).fsPath;
            const fileName = path.basename(filePath, path.extname(filePath));

            // Script files are always created with the name in the original casing.
            return this.createScriptFile(fileName, uri);
        }

        const name = this._fileList.getKeyFromValue(uri);
        return this.getScriptFileByName(name);
    }

    public getScriptFileNameExists(name: string) {
        return this._fileList.hasKey(name.toLowerCase());
    }

    public getScriptFileByName(name: string) {
        const lowerCaseName = name.toLowerCase();

        if (!this._fileList.hasKey(lowerCaseName)) {
            return null;
        }

        if (!this._scriptFiles.has(lowerCaseName)) {
            const uri = this._fileList.getValueFromKey(lowerCaseName);
            this._scriptFiles.set(
                lowerCaseName,
                // Script files are always created with the name in the original casing.
                this.createScriptFile(name, uri)
            );
        }

        return this._scriptFiles.get(lowerCaseName);
    }

    public getTypeForName(name: string, imports: string[] = []) {
        for (const importPath of imports) {
            const type = this.getTypeForFullName(`${importPath}:${name}`);
            if (type) {
                return type;
            }
        }

        return this.getTypeForFullName(name);
    }

    private getTypeForFullName(fullName: string) {
        const lowerCaseName = fullName.toLowerCase();

        if (intrinsicTypes.has(lowerCaseName)) {
            return intrinsicTypes.get(lowerCaseName);
        }

        if (this.getScriptFileNameExists(lowerCaseName)) {
            return this.getScriptFileByName(lowerCaseName).type;
        }

        const nameParts = getNameParts(fullName);

        const parentScriptFile = this.getScriptFileByName(nameParts.namespace);
        if (parentScriptFile) {
            return parentScriptFile.type.structTypes.get(
                nameParts.name.toLowerCase()
            );
        }

        return null;
    }

    public get displayTextEmitter() {
        return this._displayTextEmitter;
    }

    private createScriptFile(scriptName: string, uri: string) {
        return new ScriptFile(scriptName, uri, this, this._scriptTextProvider);
    }
}

export interface NameParts {
    fullName: string;
    namespace: string;
    name: string;
}

export function getNameParts(fullName: string): NameParts {
    const elements = fullName.split(':');
    const name = elements.pop();
    const namespace = elements.join(':');

    return {
        fullName,
        name,
        namespace,
    };
}
