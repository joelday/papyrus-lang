import { DebugProtocol as DAP } from '@vscode/debugprotocol';
import { StarfieldDebugProtocol as SFDAP } from './StarfieldDebugProtocol';
import { StarfieldConstants as SFC } from './StarfieldConstants';
export interface IThreadNode extends DAP.Thread {
    readonly id: number;
    readonly name: string;
    readonly stackFrames: IStackFrameNode[];
    readonly getDAPThread: () => DAP.Thread;
    //readonly globalScope: IScopeNode; // TODO: Do globals
}
export class ThreadNode implements IThreadNode {
    readonly id: number;
    name: string;
    stackFrames: StackFrameNode[] = [];
    constructor(thread: SFDAP.Thread) {
        this.id = thread.id;
        this.name = thread.name;
    }
    getDAPThread(): DAP.Thread {
        return {
            id: this.id,
            name: this.name,
        };
    }
}
export interface IFunctionInfo {
    readonly objectName: string;
    readonly stateName: string;
    readonly functionName: string;
}
export interface IStackFrameNode extends DAP.StackFrame {
    readonly id: number;
    readonly name: string;
    readonly source?: DAP.Source;
    readonly line: number;
    readonly column: number;
    readonly object: string;
    readonly threadId: number;
    readonly realStackIndex: number;
    readonly functionInfo: IFunctionInfo;
    readonly scopes: IScopeNode[];
    readonly getDAPStackFrame: (lineStartsAt1?: boolean, columnStartsAt1?: boolean) => DAP.StackFrame;
}
export class StackFrameNode implements IStackFrameNode {
    readonly id: number;
    readonly name: string;
    readonly source?: DAP.Source;
    readonly line: number;
    readonly column: number;
    readonly endLine?: number;
    readonly endColumn?: number;
    readonly canRestart?: boolean;
    readonly instructionPointerReference?: string;
    readonly moduleId?: number | string;
    readonly presentationHint?: 'normal' | 'label' | 'subtle';
    readonly object: string;
    readonly threadId: number;
    readonly realStackIndex: number;
    scopes: IScopeNode[] = [];
    public get functionInfo(): IFunctionInfo {
        return StackFrameNode.getFunctionInfo(this.name);
    }
    public static GetUniqueStackFrameId(threadId: number, stackindex: number) {
        return threadId * 1000 + stackindex;
    }
    public static getFunctionInfo(name: string) {
        // stackframe name is in the format of `objectname.statename.function(...)`
        // if it's the global state, it's `objectname..function(...)`; two dots indicate blank state name
        const parts = name.replace('(...)', '').split('.');
        if (parts.length < 3) {
            return {
                objectName: '',
                stateName: '',
                functionName: '',
            };
        }
        let functionName = parts[2];
        if (functionName.startsWith('::remote_')) {
            functionName = functionName.substring(8);
            if (functionName.indexOf('_')) {
                const idx = functionName.indexOf('_');
                // replace the first `_` with '.' using slice
                // No base forms with events have a `_` in them, so this is safe
                functionName = functionName.slice(0, idx) + '.' + functionName.slice(idx + 1);
            }
        }
        return {
            objectName: parts[0],
            stateName: parts[1],
            functionName: parts[2],
        };
    }
    public get DAPStackFrame(): DAP.StackFrame {
        return this.getDAPStackFrame();
    }
    public getDAPStackFrame(lineStartsAt1: boolean = true, ColumnStartsAt1: boolean = true): DAP.StackFrame {
        return {
            id: this.id,
            name: this.name,
            source: this.source,
            line: this.line + (lineStartsAt1 ? 0 : -1),
            column: this.column + (ColumnStartsAt1 ? 0 : -1),
            endLine: this.endLine ? this.endLine + (lineStartsAt1 ? 0 : -1) : undefined,
            endColumn: this.endColumn ? this.endColumn + (ColumnStartsAt1 ? 0 : -1) : undefined,
            canRestart: this.canRestart,
            instructionPointerReference: this.instructionPointerReference,
            moduleId: this.moduleId,
            presentationHint: this.presentationHint,
        };
    }
    /**
     * @param frame The stack frame to create a node for
     * @param threadId The thread id of the stack frame
     * @param stackIndex The index of the stack frame in the thread (i.e. top is 0, next is 1, etc.)
     */
    constructor(frame: SFDAP.StackFrame, threadId: number, stackIndex: number, source?: DAP.Source) {
        this.id = StackFrameNode.GetUniqueStackFrameId(threadId, stackIndex);
        this.name = frame.name;
        this.source = source;
        this.line = frame.line || 1;
        this.column = 1;
        this.moduleId = frame.object;
        this.object = frame.object;
        this.threadId = threadId;
        this.realStackIndex = stackIndex;
    }
}

export type ScopeType = 'localEnclosure' | 'localVar' | 'self' | 'parent' | 'objectMember' | 'reflectionItem';

export interface IScopeNode {
    name: string;
    scopePresentationHint?: string;
    variablesReference: number;
    frameId: number;
    namedVariables?: number;
    indexedVariables?: number;
    expensive: boolean;
    source?: DAP.Source;
    line?: number;
    column?: number;
    endLine?: number;
    endColumn?: number;

    parentScopeVarRef: number;

    getDAPScope: (lineStartsAt1?: boolean, columnStartsAt1?: boolean) => DAP.Scope;
    path: string[];
    scopeType: ScopeType;
    hasStackFrame: boolean;
    children: IVariableNode[];
}

export class ScopeNode implements IScopeNode {
    readonly name: string;
    readonly scopePresentationHint?: string;
    readonly variablesReference: number;
    readonly frameId: number;
    readonly namedVariables?: number;
    readonly indexedVariables?: number;
    readonly expensive: boolean;
    readonly source?: DAP.Source;
    readonly line?: number;
    readonly column?: number;
    readonly endLine?: number;
    readonly endColumn?: number;

    readonly path: string[];
    readonly parentScopeVarRef: number;
    readonly scopeType: ScopeType;

    readonly hasStackFrame: boolean;
    children: IVariableNode[] = [];

    public getDAPScope(lineStartsAt1: boolean = true, ColumnStartsAt1: boolean = true): DAP.Scope {
        return {
            name: this.name,
            presentationHint: this.scopePresentationHint,
            variablesReference: this.variablesReference,
            expensive: this.expensive,
            source: this.source,
            line: this.line ? this.line + (lineStartsAt1 ? 0 : -1) : undefined,
            column: this.column ? this.column + (ColumnStartsAt1 ? 0 : -1) : undefined,
            endLine: this.endLine ? this.endLine + (lineStartsAt1 ? 0 : -1) : undefined,
            endColumn: this.endColumn ? this.endColumn + (ColumnStartsAt1 ? 0 : -1) : undefined,
        } as DAP.Scope;
    }

    constructor(c: IScopeNode) {
        this.name = c.name;
        this.scopePresentationHint = c.scopePresentationHint;
        this.variablesReference = c.variablesReference;
        this.frameId = c.frameId;
        this.path = c.path;
        this.parentScopeVarRef = c.parentScopeVarRef;
        this.scopeType = c.scopeType;
        this.expensive = c.expensive;
        this.hasStackFrame = c.hasStackFrame;
    }
}

export class ScopeFactory {
    public static ScopeFromStackFrame(
        stackFrame: StackFrameNode,
        varRef: number,
        makeLocal: boolean = false,
        parentVarRef = 0
    ) {
        const scope = {
            name: makeLocal ? 'Local' : 'Self',
            scopePresentationHint: makeLocal ? 'locals' : undefined,
            variablesReference: varRef,
            expensive: false,
        } as DAP.Scope;

        if (scope.name == 'Self' && stackFrame.object) {
            scope.name = 'Self' + stackFrame.object;
        }
        return new ScopeNode({
            name: scope.name,
            scopePresentationHint: scope.presentationHint,
            variablesReference: scope.variablesReference,
            frameId: stackFrame.id,
            path: scope.name == 'Local' ? [] : ['self'],
            parentScopeVarRef: parentVarRef,
            scopeType: scope.name == 'Local' ? 'localEnclosure' : 'self',
            expensive: scope.expensive,
            hasStackFrame: true,
        } as IScopeNode);
    }
}

export interface IVariableNode {
    name: string;
    value: string;
    type?: string;
    varPresentationHint?: DAP.VariablePresentationHint;
    evaluateName?: string;
    variablesReference: number;
    namedVariables?: number;
    indexedVariables?: number;
    memoryReference?: string;

    getDAPVariable: (lineStartsAt1?: boolean, columnStartsAt1?: boolean) => DAP.Variable;
    isProp: boolean;
    realName: string;
    compound: boolean;
    parentScopeVarRef: number;
}

export interface IScopedVariableNode extends IVariableNode, IScopeNode {
    isFakeReflectionVar: boolean;
    reflectionInfo?: SFDAP.Root;
    baseForm?: string;
}

export class BasicVariableNode implements IVariableNode {
    readonly compound: boolean = false;

    readonly name: string;
    readonly value: string;
    readonly type: string;
    readonly varPresentationHint?: DAP.VariablePresentationHint;
    readonly variablesReference: number = 0;
    readonly evaluateName?: string;
    readonly namedVariables?: number = 0;
    readonly indexedVariables?: number = 0;
    readonly memoryReference?: string;

    readonly parentScopeVarRef: number;
    readonly isProp: boolean;
    readonly realName: string;

    public getDAPVariable(_lineStartsAt1?: boolean, _columnStartsAt1?: boolean): DAP.Variable {
        return {
            name: this.name,
            value: this.value,
            type: this.type,
            presentationHint: this.varPresentationHint,
            evaluateName: this.evaluateName,
            variablesReference: this.variablesReference,
            namedVariables: this.namedVariables,
            indexedVariables: this.indexedVariables,
            memoryReference: this.memoryReference,
        } as DAP.Variable;
    }

    constructor(ivarNode: IVariableNode) {
        this.name = ivarNode.name;
        this.value = ivarNode.value;
        this.type = ivarNode.type || ''; // TODO: Fix this
        this.varPresentationHint = ivarNode.varPresentationHint;
        this.evaluateName = ivarNode.evaluateName;
        this.memoryReference = ivarNode.memoryReference;
        this.parentScopeVarRef = ivarNode.parentScopeVarRef;
        this.isProp = ivarNode.isProp;
        this.realName = ivarNode.realName;

        this.variablesReference = ivarNode.variablesReference;
        this.namedVariables = ivarNode.namedVariables;
        this.indexedVariables = ivarNode.indexedVariables;
    }
}

export class ScopedVariableNode extends BasicVariableNode implements IScopedVariableNode {
    readonly compound: boolean = true;

    readonly expensive: boolean = false;
    readonly source?: DAP.Source;
    readonly line?: number;
    readonly column?: number;
    readonly endLine?: number;
    readonly endColumn?: number;

    readonly scopePresentationHint?: string | undefined;
    readonly frameId: number;
    readonly path: string[];
    readonly scopeType: ScopeType;
    readonly hasStackFrame: boolean;
    readonly children: IVariableNode[] = [];

    readonly baseForm?: string;
    readonly reflectionInfo?: SFDAP.Root;
    readonly isFakeReflectionVar: boolean;

    public getDAPScope(lineStartsAt1: boolean = true, ColumnStartsAt1: boolean = true): DAP.Scope {
        return {
            name: this.name,
            presentationHint: this.scopePresentationHint,
            variablesReference: this.variablesReference,
            expensive: this.expensive,
            source: this.source,
            line: this.line ? this.line + (lineStartsAt1 ? 0 : -1) : undefined,
            column: this.column ? this.column + (ColumnStartsAt1 ? 0 : -1) : undefined,
            endLine: this.endLine ? this.endLine + (lineStartsAt1 ? 0 : -1) : undefined,
            endColumn: this.endColumn ? this.endColumn + (ColumnStartsAt1 ? 0 : -1) : undefined,
        } as DAP.Scope;
    }

    constructor(scopedVarNode: IScopedVariableNode) {
        super(scopedVarNode);
        // dap.scope
        this.source = scopedVarNode.source;
        this.line = scopedVarNode.line;
        this.column = scopedVarNode.column;
        this.endLine = scopedVarNode.endLine;
        this.endColumn = scopedVarNode.endColumn;
        this.scopePresentationHint = scopedVarNode.scopePresentationHint;

        // iscopenode
        this.frameId = scopedVarNode.frameId;
        this.path = scopedVarNode.path || [];
        this.scopeType = scopedVarNode.scopeType;
        this.hasStackFrame = scopedVarNode.hasStackFrame;
        this.children = scopedVarNode.children || [];

        // iscopedvariablenode
        this.baseForm = scopedVarNode.baseForm;
        this.reflectionInfo = scopedVarNode.reflectionInfo;
        this.isFakeReflectionVar = scopedVarNode.isFakeReflectionVar;
    }
}

export class VariableNodeFactory {
    protected static makeiVariableNode(
        variable: SFDAP.Variable,
        varRef: number,
        parentScope?: ScopeNode
    ): IVariableNode {
        let name = variable.name;
        let varPresentationHint: DAP.VariablePresentationHint | undefined = undefined;
        let isProp = false;
        const realName = variable.name;
        if (variable.name.startsWith('::') && variable.name.endsWith('_var')) {
            name = variable.name.substring(2, variable.name.length - 4);
            varPresentationHint = {
                kind: 'property',
            };
            isProp = true;
        }
        const path = [...(parentScope?.path || []), realName];
        const _evaluateName = path.join('.');

        return {
            name: name,
            value: variable.value,
            type: variable.type,
            varPresentationHint: varPresentationHint,
            evaluateName: _evaluateName, // TODO: this?
            variablesReference: varRef,
            namedVariables: undefined,
            indexedVariables: undefined,
            memoryReference: undefined,
            parentScopeVarRef: parentScope?.variablesReference || 0,
            isProp: isProp,
            realName: realName,
            compound: variable.compound,
        } as IVariableNode;
    }

    protected static iScopeFromVariable(varNode: IVariableNode, frameId?: number, parentScope?: ScopeNode) {
        if (!varNode.compound) {
            return undefined;
        }
        let _scopeType: ScopeType = 'objectMember';
        if (varNode.name == 'self' && parentScope?.scopeType == 'localEnclosure') {
            _scopeType = 'self';
        } else if (varNode.name.toLowerCase() == 'parent') {
            _scopeType = 'parent';
        } else if (parentScope?.scopeType == 'localEnclosure') {
            _scopeType = 'localVar';
        }
        return {
            name: varNode.name,
            variablesReference: varNode.variablesReference,
            frameId: frameId || 0,
            path: [...(parentScope?.path || []), varNode.realName],
            scopeType: _scopeType,
            parentScopeVarRef: parentScope?.variablesReference || 0,
            expensive: false,
            hasStackFrame: frameId && frameId !== undefined && frameId > 0,
        } as IScopeNode;
    }

    public static makeVariableNode(
        variable: SFDAP.Variable,
        varRef: number,
        parentScope?: ScopeNode,
        frameId?: number,
        isFakeReflectionVar?: boolean
    ): IVariableNode {
        if (variable.compound) {
            return VariableNodeFactory.makeScopedVariableNode(
                variable,
                varRef,
                parentScope,
                frameId,
                isFakeReflectionVar
            );
        }

        return new BasicVariableNode(VariableNodeFactory.makeiVariableNode(variable, varRef, parentScope));
    }

    public static makeScopedVariableNode(
        variable: SFDAP.Variable,
        varRef: number,
        parentScope?: ScopeNode,
        frameId?: number,
        _isFakeReflectionVar?: boolean
    ): IScopedVariableNode {
        const iVarNode = VariableNodeFactory.makeiVariableNode(variable, varRef, parentScope);
        const iScopeNode = VariableNodeFactory.iScopeFromVariable(iVarNode, frameId, parentScope)!;
        const refInfo = SFC.parseOutReflectionInfo(variable.value);
        const reflectionInfo = (refInfo?.root as SFDAP.Root) || undefined;
        const baseForm = refInfo?.baseForm || undefined;

        let _scopeType: ScopeType = 'objectMember';
        if (variable.name.toLowerCase() == 'self' && parentScope?.scopeType == 'localEnclosure') {
            _scopeType = 'self';
        } else if (variable.name.toLowerCase() == 'parent') {
            _scopeType = 'parent';
        } else if (parentScope?.scopeType == 'localEnclosure') {
            _scopeType = 'localVar';
        }
        const iSVarNode: IScopedVariableNode = {
            ...iVarNode,
            ...iScopeNode,
            reflectionInfo: reflectionInfo,
            baseForm: baseForm,
            isFakeReflectionVar: _isFakeReflectionVar || false,
            // expensive: false,
            // scopeType: _scopeType,
            // frameId: frameId || 0,
        };

        return new ScopedVariableNode(iSVarNode);
    }
}

export interface IStateNode {}

export class StackFrameMap extends Map<number, StackFrameNode> {}
export class ScopeMap extends Map<number, IScopeNode> {}

export class VariableMap extends Map<number, IVariableNode> {}

export class StateNode implements IStateNode {
    private readonly DUMMY_THREAD_NAME = 'DUMMY THREAD';
    private readonly DUMMY_THREAD_ID = 0;
    private readonly DUMMY_THREAD_OBJ: ThreadNode = new ThreadNode({
        id: this.DUMMY_THREAD_ID,
        name: this.DUMMY_THREAD_NAME,
    } as SFDAP.Thread);
    private readonly DUMMY_ARR = [this.DUMMY_THREAD_OBJ];
    private _threadMap: Map<number, ThreadNode> = new Map<number, ThreadNode>();
    private _stackFrameMap: StackFrameMap = new StackFrameMap();
    // private _stackIdToThreadIdMap: Map<number, number> = new Map<number, number>();
    private _scopeMap: ScopeMap = new ScopeMap();
    private _variableMap: VariableMap = new VariableMap();
    // private _variableReferencetoFrameIdMap: Map<number, number> = new Map<number, number>();
    private _variableRefCount = 0;
    constructor() {}
    public clear() {
        this._threadMap = new Map<number, ThreadNode>();
        this._stackFrameMap = new StackFrameMap();
        this._scopeMap = new ScopeMap();
        this._variableMap = new VariableMap();
        this._variableRefCount = 0;
    }
    public getVariableRefCount() {
        this._variableRefCount++;
        return this._variableRefCount;
    }

    public hasThread(threadId: number): boolean {
        return this._threadMap.has(threadId);
    }
    public getThread(threadId: number): DAP.Thread {
        return this.getThreadNode(threadId).getDAPThread();
    }

    protected getThreadNode(threadId: number): ThreadNode {
        if (!this._threadMap.has(threadId)) {
            throw new Error(`Thread ${threadId} not found`);
        }
        return this._threadMap.get(threadId)!;
    }

    public hasStackFrame(stackId: number): boolean {
        return this._stackFrameMap.has(stackId);
    }
    public getStackFrame(stackId: number): DAP.StackFrame {
        return this.getStackFrameNode(stackId)!.getDAPStackFrame();
    }

    protected getStackFrameNode(stackId: number): StackFrameNode {
        if (!this.hasStackFrame(stackId)) {
            throw new Error(`StackId ${stackId} not found`);
        }
        return this._stackFrameMap.get(stackId)!;
    }

    protected getScopeNode(varRef: number): IScopeNode {
        if (varRef > this._variableRefCount || !this.hasScope(varRef)) {
            throw new Error(`Variable reference ${varRef} is out of range`);
        }
        return this._scopeMap.get(varRef)!;
    }
    public hasScope(varRef: number): boolean {
        return this._scopeMap.has(varRef);
    }
    public getScope(varRef: number): DAP.Scope {
        return this.getScopeNode(varRef)!.getDAPScope();
    }
    public getFrameIdForScope(varRef: number): number {
        return this.hasScope(varRef) ? this.getScopeNode(varRef).frameId : 0;
    }

    public hasVariable(varRef: number): boolean {
        return this._variableMap.has(varRef);
    }

    public getVariable(varRef: number): DAP.Variable {
        return this.getVariableNode(varRef).getDAPVariable();
    }

    protected getVariableNode(varRef: number): IVariableNode {
        if (varRef > this._variableRefCount || !this.hasVariable(varRef)) {
            throw new Error(`Variable reference ${varRef} is out of range`);
        }
        return this._variableMap.get(varRef)!;
    }
    protected addThreadToThreadMap(thread: ThreadNode) {
        this._threadMap.set(thread.id, thread);
    }

    public addThread(threadId: number, threadName?: string) {
        if (this.hasThread(threadId)) {
            if (threadName) {
                this.getThreadNode(threadId).name = threadName;
            }
            return;
        }
        if (!threadName) {
            threadName = '<thread ' + threadId.toString() + '>';
        }
        const threadNode = new ThreadNode({ id: threadId, name: threadName } as DAP.Thread);
        this.addThreadToThreadMap(threadNode);
    }
    public addThreads(threads: SFDAP.Thread[]) {
        for (const thread of threads) {
            this.addThread(thread.id, thread.name);
        }
    }
    public setThreads(threads: SFDAP.Thread[]) {
        // first, we need to remove any threads that are no longer in the list
        const threadIds = threads.map((t) => t.id);
        const threadIdsToRemove = Array.from(this._threadMap.keys()).filter((t) => !threadIds.includes(t));
        for (const threadId of threadIdsToRemove) {
            this.removeThread(threadId);
        }
        // then add the new ones
        this.addThreads(threads);
    }

    public removeThread(threadId: number) {
        this._threadMap.delete(threadId);
    }

    public getThreads(): DAP.Thread[] {
        if (this._threadMap.size == 0) {
            return this.DUMMY_ARR;
        }
        return Array.from(this._threadMap.values()).map((t) => t.getDAPThread());
    }

    protected addStackFrameToStackFrameMap(stackFrame: StackFrameNode) {
        this.getThreadNode(stackFrame.threadId).stackFrames.push(stackFrame);
        this._stackFrameMap.set(stackFrame.id, stackFrame);
    }

    public addStackFrame(stackFrame: SFDAP.StackFrame, threadId: number, stackIndex: number, source?: DAP.Source) {
        const stackFrameNode = new StackFrameNode(stackFrame, threadId, stackIndex, source);
        this.addStackFrameToStackFrameMap(stackFrameNode);
        return stackFrameNode.DAPStackFrame;
    }

    public getThreadIdForStackFrame(stackId: number): number {
        return Math.floor(stackId / 1000); // TODO: replace this
    }

    protected addScopeToScopeMap(scope: IScopeNode) {
        if (scope.hasStackFrame) {
            this.getStackFrameNode(scope.frameId)?.scopes.push(scope);
        }
        this._scopeMap.set(scope.variablesReference, scope);
    }

    protected makeLocalScopeNodeForStackFrame(frameId: number): IScopeNode | undefined {
        const frame = this.getStackFrameNode(frameId)!;
        const localScope = ScopeFactory.ScopeFromStackFrame(frame, this.getVariableRefCount(), true);
        this.addScopeToScopeMap(localScope);
        return localScope;
    }
    public makeLocalScopeForStackFrame(frameId: number): DAP.Scope {
        if (this.hasStackFrame(frameId)) {
            return this.makeLocalScopeNodeForStackFrame(frameId)!.getDAPScope();
        }
        throw new Error(`Stack frame ${frameId} not found`);
    }

    public getVariablesArgumentsForScope(varRef: number): SFDAP.VariablesArguments {
        // TODO: handle non-local scopes
        const scope = this.getScopeNode(varRef);
        const frame = this.getStackFrameNode(scope.frameId);
        const thread = this.getThreadNode(frame.threadId);

        const root: SFDAP.Root = {
            type: 'stackFrame',
            threadId: thread.id,
            stackFrameIndex: frame.realStackIndex,
        };
        return {
            root: root,
            path: scope.path,
        };
    }

    protected getSelfScopeRef(frameid: number): number | undefined {
        if (this._stackFrameMap.has(frameid)) {
            const frame = this._stackFrameMap.get(frameid)!;
            const selfScope = frame.scopes.find((s) => s.name == 'Self');
            return selfScope?.variablesReference || undefined;
        }
        return undefined;
    }
    protected addVariableToState(varNode: IVariableNode, parentScopeVarRef?: number) {
        const parentScope = parentScopeVarRef ? this.getScopeNode(parentScopeVarRef) : undefined;
        if (parentScope) {
            parentScope?.children.push(varNode);
        }
        if (varNode.compound) {
            this.addScopeToScopeMap(varNode as IScopedVariableNode);
            this._variableMap.set(varNode.variablesReference, varNode);
        }
    }

    protected addVariableNodesToState(
        variables: SFDAP.Variable[],
        parentScopeVarRef?: number,
        frameid?: number
    ): IVariableNode[] {
        const newVariables = [];
        if (frameid && !this._stackFrameMap.has(frameid)) {
            throw new Error(`Stack frame ${frameid} not found`);
        }
        if (parentScopeVarRef && !this._scopeMap.has(parentScopeVarRef)) {
            throw new Error(`Parent scope ${parentScopeVarRef} not found`);
        }

        const parentScope = parentScopeVarRef ? this.getScopeNode(parentScopeVarRef) : undefined;

        for (const oldVar of variables) {
            const varNode = VariableNodeFactory.makeVariableNode(
                oldVar,
                oldVar.compound ? this.getVariableRefCount() : 0, // TODO: make variable node factory handle this
                parentScope,
                frameid
            );
            this.addVariableToState(varNode, parentScopeVarRef);
            newVariables.push(varNode);
        }
        return newVariables;
    }

    public addVariablesToState(
        variables: SFDAP.Variable[],
        parentScopeVarRef?: number,
        frameid?: number
    ): DAP.Variable[] {
        return this.addVariableNodesToState(variables, parentScopeVarRef, frameid).map((v) => v.getDAPVariable());
    }
}
