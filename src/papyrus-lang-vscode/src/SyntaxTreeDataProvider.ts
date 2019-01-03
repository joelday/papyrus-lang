import { ActiveDocTreeDataProviderBase } from './common/view/ActiveDocTreeDataProviderBase';
import { SyntaxTreeDataNode } from './SyntaxTreeDataNode';
import { ClientServer } from './ClientServer';

export class SyntaxTreeDataProvider extends ActiveDocTreeDataProviderBase {
    private readonly _client: ClientServer;

    constructor(client: ClientServer) {
        super();
        this._client = client;
    }

    protected refresh(): void {
        this.treeDataChanged();
    }

    protected async getRootChildren(): Promise<SyntaxTreeDataNode[]> {
        if (!this.currentDocument) {
            return;
        }

        const data = await this._client.requestSyntaxTree(this.currentDocument.uri.toString());
        if (!data) {
            return [];
        }

        const nodes: SyntaxTreeDataNode[] = [];

        if (data.root) {
            nodes.push(new SyntaxTreeDataNode(data.root, null));
        }

        return nodes;
    }

    get currentDocumentSupported() {
        return this.currentDocument.languageId === 'papyrus';
    }
}
