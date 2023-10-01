import * as vs from 'vscode';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export abstract class TreeViewControllerBase<T = any> implements vs.Disposable {
    private readonly _viewId: string;
    private readonly _dataProvider: vs.TreeDataProvider<T>;

    private _view: vs.TreeView<T>;

    constructor(viewId: string, dataProvider: vs.TreeDataProvider<T>) {
        this._viewId = viewId;
        this._dataProvider = dataProvider;

        this._view = vs.window.createTreeView(this._viewId, { treeDataProvider: this._dataProvider });
    }

    protected get view() {
        return this._view;
    }

    protected get viewId() {
        return this._viewId;
    }

    protected get dataProvider() {
        return this._dataProvider;
    }

    dispose() {
        this._view!.dispose();
    }
}
