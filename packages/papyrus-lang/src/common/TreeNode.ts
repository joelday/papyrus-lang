import { StringBuilder } from './StringBuilder';
import { iterateWhere, stringOf } from './Utilities';

export interface TreeNode<T extends TreeNode<T>> {
    readonly children: ReadonlyArray<T>;
}

export interface ParentLinkedTreeNode<T extends ParentLinkedTreeNode<T>>
    extends TreeNode<T> {
    readonly parent: this;
}

export interface TreeVisitationState<T extends TreeNode<T>> {
    readonly node: T;
    readonly depth: number;
    skipChildren: boolean;
}

function* visitTreeInternal<T extends TreeNode<T>>(
    rootNode: T,
    depth: number = 0
): IterableIterator<TreeVisitationState<T>> {
    const state = { node: rootNode, depth, skipChildren: false };
    yield state;

    if (!rootNode.children || state.skipChildren) {
        return;
    }

    for (const child of rootNode.children) {
        for (const subChild of visitTreeInternal<T>(child, depth + 1)) {
            yield subChild;
        }
    }
}

export function visitTree<T extends TreeNode<T>>(rootNode: T) {
    return visitTreeInternal(rootNode);
}

export function visitTreeNodesWhere<T extends TreeNode<T>>(
    rootNode: T,
    filter: (node: TreeVisitationState<T>) => boolean
) {
    return iterateWhere(visitTree(rootNode), filter);
}

export function visitLeafNodes<T extends TreeNode<T>>(rootNode: T) {
    return iterateWhere(
        visitTree(rootNode),
        (n) => !n.node.children || Array.from(n.node.children).length === 0
    );
}

export function isDescendentOfNodeOrSelf<T extends ParentLinkedTreeNode<T>>(
    node: T,
    ancestor: T
) {
    if (node === ancestor) {
        return true;
    }

    return isDescendentOfNode(node, ancestor);
}

export function isDescendentOfNode<T extends ParentLinkedTreeNode<T>>(
    node: T,
    ancestor: T
) {
    for (const nodeAncestor of visitAncestors(node)) {
        if (ancestor === nodeAncestor) {
            return true;
        }
    }

    return false;
}

export function* visitAncestors<T extends ParentLinkedTreeNode<T>>(
    node: T,
    includingFirst: boolean = false
) {
    let parent = includingFirst ? node : node.parent;

    while (parent) {
        yield parent;
        parent = parent.parent;
    }
}

export function findFirstParent<T extends ParentLinkedTreeNode<T>>(
    node: T,
    find: (node: T) => boolean,
    includingFirst: boolean = false
) {
    for (const parent of visitAncestors(node, includingFirst)) {
        if (find(parent)) {
            return parent;
        }
    }

    return null;
}

export function treeToString(rootNode: TreeNode<any>) {
    const encountered = new Set<TreeNode<any>>();
    const sb = new StringBuilder();

    for (const node of visitTree(rootNode)) {
        sb.append(stringOf('    ', node.depth));

        if (encountered.has(node.node)) {
            sb.appendLine('(circular)');
        } else {
            encountered.add(node.node);
            sb.appendLine(node.node.toString());
        }
    }

    return sb.toString();
}
