import { TreeDataProviderBase } from '../../common/vscode/view/TreeDataProviderBase';
import { TreeDataNode } from '../../common/vscode/view/TreeDataNode';
import { TreeItem, TreeItemCollapsibleState, Uri } from 'vscode';
import { ILanguageClientManager } from '../../server/LanguageClientManager';
import { Observable, combineLatest, Subscription } from 'rxjs';
import { mergeMap, take } from 'rxjs/operators';
import {
    ProjectInfos,
    ProjectInfo,
    ProjectInfoSourceInclude,
    ProjectInfoScript,
} from '../../server/messages/ProjectInfos';
import { PapyrusGame, getShortDisplayNameForGame } from '../../PapyrusGame';

export class GameTreeDataNode implements TreeDataNode {
    private readonly _game: PapyrusGame;
    private readonly _projects: ProjectInfo[];

    constructor(game: PapyrusGame, projects: ProjectInfo[]) {
        this._game = game;
        this._projects = projects;
    }

    async getParent(): Promise<TreeDataNode> {
        return null;
    }

    getTreeItem(): TreeItem {
        return new TreeItem(getShortDisplayNameForGame(this._game), TreeItemCollapsibleState.Expanded);
    }

    async getChildren(): Promise<TreeDataNode[]> {
        return this._projects.map((p) => new ProjectTreeDataNode(p, this));
    }
}

export class ProjectTreeDataNode implements TreeDataNode {
    private readonly _parent: GameTreeDataNode;
    private readonly _project: ProjectInfo;

    constructor(project: ProjectInfo, parent: GameTreeDataNode) {
        this._parent = parent;
        this._project = project;
    }

    getParent() {
        return this._parent;
    }

    getTreeItem() {
        return new TreeItem(this._project.name, TreeItemCollapsibleState.Expanded);
    }

    async getChildren(): Promise<TreeDataNode[]> {
        return this._project.sourceIncludes.map((s) => new ProjectSourceIncludeTreeDataNode(s, this)).reverse();
    }
}

export class ProjectSourceIncludeTreeDataNode implements TreeDataNode {
    private readonly _parent: ProjectTreeDataNode;
    private readonly _sourceInclude: ProjectInfoSourceInclude;

    constructor(sourceInclude: ProjectInfoSourceInclude, parent: ProjectTreeDataNode) {
        this._parent = parent;
        this._sourceInclude = sourceInclude;
    }

    getParent() {
        return this._parent;
    }

    getTreeItem() {
        const treeItem = new TreeItem(
            this._sourceInclude.fullPath ? Uri.file(this._sourceInclude.fullPath) : ('Scripts' as any),
            TreeItemCollapsibleState.Expanded
        );

        return treeItem;
    }

    async getChildren(): Promise<TreeDataNode[]> {
        return this._sourceInclude.scripts.map((s) => new ProjectScriptTreeDataNode(s, this));
    }
}

export class ProjectScriptTreeDataNode implements TreeDataNode {
    private readonly _parent: ProjectSourceIncludeTreeDataNode;
    private readonly _script: ProjectInfoScript;

    constructor(script: ProjectInfoScript, parent: ProjectSourceIncludeTreeDataNode) {
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

export class ProjectsTreeDataProvider extends TreeDataProviderBase {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _projectInfosSubscription: Subscription;
    private readonly _projectInfosObservables: [PapyrusGame, Observable<ProjectInfos>][];

    constructor(@ILanguageClientManager languageClientManager: ILanguageClientManager) {
        super();

        this._languageClientManager = languageClientManager;

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

        return withProjects.map((p) => new GameTreeDataNode(p[0], p[1].projects));
    }

    dispose() {
        super.dispose();

        this._projectInfosSubscription.unsubscribe();
    }
}
