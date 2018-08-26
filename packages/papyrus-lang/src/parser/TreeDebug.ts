import { StringBuilder } from '../common/StringBuilder';
import { visitTree } from '../common/TreeNode';
import { stringOf } from '../common/Utilities';
import { Node, NodeObject } from './Node';

export function printNodeTree(rootNode: Node) {
    const sb = new StringBuilder();
    let lastDepth = 0;

    for (const node of visitTree(rootNode)) {
        if (!node.node) {
            continue;
        }

        if (node.depth < lastDepth) {
            sb.appendLine();
        }

        lastDepth = node.depth;

        sb.append(stringOf('    ', node.depth));
        sb.appendLine(node.node.toString());

        if (node.node.children.length === 0) {
            sb.append(stringOf('    ', node.depth + 1));
            sb.appendLine(`'${((node.node as any) as NodeObject).sourceText}'`);
        }
    }

    return sb.toString();
}
