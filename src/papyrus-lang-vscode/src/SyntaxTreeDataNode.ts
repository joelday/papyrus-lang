import * as vs from 'vscode';

import { TreeDataNode } from './common/view/TreeDataNode';
import { DocumentSyntaxTreeNode } from './ClientServer';

export class SyntaxTreeDataNode implements TreeDataNode {
    private readonly _node: DocumentSyntaxTreeNode;
    private readonly _parent: SyntaxTreeDataNode;

    constructor(node: DocumentSyntaxTreeNode, parent: SyntaxTreeDataNode) {
        this._node = node;
        this._parent = parent;
    }

    getParent() {
        return this._parent;
    }

    getTreeItem(): vs.TreeItem | Thenable<vs.TreeItem> {
        const { name, text, range } = this._node;

        const treeItem = new vs.TreeItem(
            `${name} (${range.start.line}:${range.start.character}, ${range.end.line}:${range.end.character})`
        );

        treeItem.tooltip = text;

        treeItem.collapsibleState =
            this._node.children.length > 0 ? vs.TreeItemCollapsibleState.Collapsed : vs.TreeItemCollapsibleState.None;

        return treeItem;
    }

    getChildren(): vs.ProviderResult<SyntaxTreeDataNode[]> {
        return Array.from(this._node.children).map((n) => new SyntaxTreeDataNode(n, this));
    }
}
