import * as vs from 'vscode';

export interface TreeDataNode {
    getParent(): vs.ProviderResult<TreeDataNode>;
    getTreeItem(): vs.TreeItem | Thenable<vs.TreeItem>;
    getChildren(): vs.ProviderResult<TreeDataNode[]>;
}
