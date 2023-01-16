import { injectable } from 'inversify';
import * as vs from 'vscode';

import { TreeDataNode } from './TreeDataNode';

@injectable()
export abstract class TreeDataProviderBase
    implements vs.TreeDataProvider<TreeDataNode>, vs.Disposable {

    private readonly _disposables: vs.Disposable[] = [];
    private readonly _onDidChangeTreeDataEventEmitter = new vs.EventEmitter<TreeDataNode | undefined>();

    get onDidChangeTreeData() {
        return this._onDidChangeTreeDataEventEmitter.event;
    }

    getTreeItem(element: TreeDataNode): vs.TreeItem | Thenable<vs.TreeItem> {
        return element.getTreeItem();
    }

    getChildren(element?: TreeDataNode): vs.ProviderResult<TreeDataNode[]> {
        return element ? element.getChildren() : this.getRootChildren();
    }

    getParent(element: TreeDataNode): vs.ProviderResult<TreeDataNode> {
        return element.getParent();
    }

    dispose() {
        vs.Disposable.from(
            this._onDidChangeTreeDataEventEmitter, ...this._disposables).dispose();
    }

    protected get disposables() {
        return this._disposables;
    }

    protected treeDataChanged(dataNode?: TreeDataNode) {
        this._onDidChangeTreeDataEventEmitter.fire(dataNode);
    }

    protected abstract getRootChildren(): vs.ProviderResult<TreeDataNode[]>;
}