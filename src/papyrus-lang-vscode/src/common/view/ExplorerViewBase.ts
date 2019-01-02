import * as vs from 'vscode';

export abstract class ExplorerViewBase<T extends vs.TreeDataProvider<any>> implements vs.Disposable {
    private readonly _viewId: string;
    private readonly _dataProvider: T;

    private _registration: vs.Disposable | null = null;
    private _treeDataSubscription: vs.Disposable | null = null;

    constructor(viewId: string, dataProvider: T) {
        this._viewId = viewId;
        this._dataProvider = dataProvider;

        this._treeDataSubscription = this._dataProvider.onDidChangeTreeData!(() => {
            this.refreshView();
        });
    }

    public register() {
        if (this._registration) {
            return;
        }

        this._registration = vs.window.registerTreeDataProvider(this._viewId, this._dataProvider);
        this.refreshView();
    }

    protected get viewId() {
        return this._viewId;
    }

    protected get dataProvider() {
        return this._dataProvider;
    }

    protected abstract get isEnabled(): boolean;

    protected refreshView() {
        if (!this._registration) {
            return;
        }

        vs.commands.executeCommand('setContext', this._viewId + ':enabled', this.isEnabled);
    }

    dispose() {
        this._treeDataSubscription!.dispose();
        this._registration!.dispose();
    }
}
