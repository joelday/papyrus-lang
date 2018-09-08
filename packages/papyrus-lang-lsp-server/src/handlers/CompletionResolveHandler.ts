import { isDescendentOfNodeOrSelf, visitAncestors } from 'papyrus-lang/lib/common/TreeNode';
import { Node, NodeKind } from 'papyrus-lang/lib/parser/Node';
import { SymbolKind } from 'papyrus-lang/lib/symbols/Symbol';
import { LookupFlags, MemberTypes } from 'papyrus-lang/lib/types/TypeChecker';
import {
    CancellationToken,
    CompletionItem,
    CompletionItemKind,
    TextDocumentPositionParams,
} from 'vscode-languageserver';
import { IDocumentHelpers } from '../DocumentHelpers';
import { getCompletionItem, getStubScriptCompletionItem } from '../features/Completions';
import { IProjectManager } from '../ProjectManager';
import { Handler } from '../RequestHandlerService';
import { papyrusRangeToRange } from '../Utilities';

export class CompletionResolveHandler implements Handler<'onCompletionResolve'> {
    private readonly _documentHelpers: IDocumentHelpers;
    private readonly _projectManager: IProjectManager;

    constructor(@IDocumentHelpers documentHelpers: IDocumentHelpers, @IProjectManager projectManager: IProjectManager) {
        this._documentHelpers = documentHelpers;
        this._projectManager = projectManager;
    }

    public handleRequest(item: CompletionItem, _cancellationToken: CancellationToken) {
        if (!item.data || !item.data.isScriptStub) {
            return item;
        }

        const projectHost = this._projectManager.projectHosts.find(
            (host) => host.program.project.filePath === item.data.projectFile
        );

        const type = projectHost.program.getTypeForName(item.data.scriptName);

        if (!type) {
            return item;
        }

        return getCompletionItem(type.symbol, projectHost.program.displayTextEmitter);
    }
}
