import { TreeViewControllerBase } from '../../common/vscode/view/TreeViewControllerBase';
import { ProjectsTreeDataProvider } from './ProjectsTreeDataProvider';
import { TreeDataNode } from '../../common/vscode/view/TreeDataNode';

export class ProjectsView extends TreeViewControllerBase<TreeDataNode> {
    constructor(dataProvider: ProjectsTreeDataProvider) {
        super('papyrus-projects', dataProvider);
    }
}
