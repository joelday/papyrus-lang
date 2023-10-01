import { TreeDataProviderBase } from '../../common/vscode/view/TreeDataProviderBase';
import { TreeDataNode } from '../../common/vscode/view/TreeDataNode';
import { TreeItem, TreeItemCollapsibleState, Uri, ExtensionContext } from 'vscode';
import { ILanguageClientManager } from '../../server/LanguageClientManager';
import { Observable, combineLatest, Subscription } from 'rxjs';
import { mergeMap, take } from 'rxjs/operators';
import {
    ProjectInfos,
    ProjectInfo,
    ProjectInfoSourceInclude,
    ProjectInfoScript,
} from '../../server/messages/ProjectInfos';
import { PapyrusGame, getShortDisplayNameForGame } from "../../PapyrusGame";
import { flatten } from '../../Utilities';
import { IExtensionContext } from '../../common/vscode/IocDecorators';
import { inject, injectable } from 'inversify';

export class GameTreeDataNode implements TreeDataNode {
    private readonly _context: ExtensionContext;
    private readonly _game: PapyrusGame;
    private readonly _projects: ProjectInfo[];

    constructor(context: ExtensionContext, game: PapyrusGame, projects: ProjectInfo[]) {
        this._context = context;
        this._game = game;
        this._projects = projects;
    }

    async getParent(): Promise<TreeDataNode | null> {
        return null;
    }

    getTreeItem(): TreeItem {
        const treeItem = new TreeItem(getShortDisplayNameForGame(this._game), TreeItemCollapsibleState.Expanded);

        treeItem.iconPath = this._context.asAbsolutePath(
            `resources/${this._game === PapyrusGame.fallout4 ? 'fallout4' : 'skyrim'}.png`
        );

        return treeItem;
    }

    async getChildren(): Promise<TreeDataNode[]> {
        return this._projects.map((p) => new ProjectTreeDataNode(this._context, p, this));
    }
}

export class ProjectTreeDataNode implements TreeDataNode {
    private readonly _context: ExtensionContext;
    private readonly _parent: GameTreeDataNode;
    private readonly _project: ProjectInfo;

    constructor(context: ExtensionContext, project: ProjectInfo, parent: GameTreeDataNode) {
        this._context = context;
        this._parent = parent;
        this._project = project;
    }

    getParent() {
        return this._parent;
    }

    getTreeItem() {
        const treeItem = new TreeItem(this._project.name, TreeItemCollapsibleState.Collapsed);
        treeItem.iconPath = this._context.asAbsolutePath('resources/ScriptManager_16x.svg');
        return treeItem;
    }

    async getChildren(): Promise<TreeDataNode[]> {
        const imports = this._project.sourceIncludes.filter((include) => include.isImport);
        const scripts = Array.from(
            flatten(this._project.sourceIncludes.filter((include) => !include.isImport).map((i) => i.scripts))
        );

        const children: (ProjectImportsTreeDataNode | ProjectScriptTreeDataNode)[] = [];

        if (imports.length > 0) {
            children.push(new ProjectImportsTreeDataNode(this._context, imports, this));
        }

        children.push(...scripts.map((s) => new ProjectScriptTreeDataNode(this._context, s, this)));

        return children;
    }
}

export class ProjectImportsTreeDataNode implements TreeDataNode {
    private readonly _context: ExtensionContext;
    private readonly _parent: ProjectTreeDataNode;
    private readonly _imports: ProjectInfoSourceInclude[];

    constructor(context: ExtensionContext, imports: ProjectInfoSourceInclude[], parent: ProjectTreeDataNode) {
        this._context = context;
        this._parent = parent;
        this._imports = imports;
    }

    getParent() {
        return this._parent;
    }

    getTreeItem() {
        const treeItem = new TreeItem('Imports', TreeItemCollapsibleState.Collapsed);
        treeItem.iconPath = this._context.asAbsolutePath('resources/ScriptGroup_16x.svg');
        return treeItem;
    }

    async getChildren(): Promise<TreeDataNode[]> {
        return this._imports.map((i) => new ProjectImportTreeDataNode(this._context, i, this));
    }
}

export class ProjectImportTreeDataNode implements TreeDataNode {
    private readonly _context: ExtensionContext;
    private readonly _parent: ProjectImportsTreeDataNode;
    private readonly _scriptImport: ProjectInfoSourceInclude;

    constructor(context: ExtensionContext, scriptImport: ProjectInfoSourceInclude, parent: ProjectImportsTreeDataNode) {
        this._context = context;
        this._parent = parent;
        this._scriptImport = scriptImport;
    }

    getParent() {
        return this._parent;
    }

    getTreeItem() {
        return new TreeItem(Uri.file(this._scriptImport.fullPath), TreeItemCollapsibleState.Collapsed);
    }

    async getChildren(): Promise<TreeDataNode[]> {
        return this._scriptImport.scripts.map((s) => new ProjectScriptTreeDataNode(this._context, s, this));
    }
}

export class ProjectScriptTreeDataNode implements TreeDataNode {
    private readonly _context: ExtensionContext;
    private readonly _parent: ProjectTreeDataNode | ProjectImportTreeDataNode;
    private readonly _script: ProjectInfoScript;

    constructor(
        context: ExtensionContext,
        script: ProjectInfoScript,
        parent: ProjectTreeDataNode | ProjectImportTreeDataNode
    ) {
        this._context = context;
        this._parent = parent;
        this._script = script;
    }

    getParent() {
        return this._parent;
    }

    getTreeItem() {
        const treeItem = new TreeItem(Uri.file(this._script.filePath), TreeItemCollapsibleState.None);

        treeItem.label = this._script.identifier;
        treeItem.command = {
            title: 'Open',
            command: 'vscode.open',
            arguments: [Uri.file(this._script.filePath)],
        };

        return treeItem;
    }

    async getChildren() {
        return [];
    }
}

@injectable()
export class ProjectsTreeDataProvider extends TreeDataProviderBase {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _context: ExtensionContext;
    private readonly _projectInfosSubscription: Subscription;
    private readonly _projectInfosObservables: [PapyrusGame, Observable<ProjectInfos>][];

    constructor(
        @inject(ILanguageClientManager) languageClientManager: ILanguageClientManager,
        @inject(IExtensionContext) context: ExtensionContext
    ) {
        super();

        this._languageClientManager = languageClientManager;
        this._context = context;

        this._projectInfosObservables = Array.from(this._languageClientManager.clients.entries()).map((pair) => {
            return [pair[0], pair[1].pipe(mergeMap((p) => p.projectInfos))] as [PapyrusGame, Observable<ProjectInfos>];
        });

        this._projectInfosSubscription = combineLatest(this._projectInfosObservables.map((p) => p[1])).subscribe(() => {
            this.treeDataChanged();
        });
    }

    protected async getRootChildren(): Promise<TreeDataNode[]> {
        const infos = await Promise.all(
            this._projectInfosObservables.map(
                async (p) => [p[0], await p[1].pipe(take(1)).toPromise()] as [PapyrusGame, ProjectInfos]
            )
        );

        const withProjects = infos.filter((p) => p[1] && p[1].projects.length > 0);

        return withProjects.map((p) => new GameTreeDataNode(this._context, p[0], p[1].projects));
    }

    dispose() {
        super.dispose();

        this._projectInfosSubscription.unsubscribe();
    }
}
