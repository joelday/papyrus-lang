import { DebugProtocol as DAP } from '@vscode/debugprotocol';
import { StarfieldDebugProtocol as SFDAP } from './StarfieldDebugProtocol';

export interface IThreadNode extends DAP.Thread {
    readonly id: number;
    readonly name: string;
    readonly stackFrames: IStackFrameNode[];
    readonly DAPThread: DAP.Thread;
}
export class ThreadNode implements IThreadNode {
    readonly id: number;
    readonly name: string;
    stackFrames: StackFrameNode[] = [];
    constructor(thread: SFDAP.Thread) {
        this.id = thread.id;
        this.name = thread.name;
    }
    get DAPThread(): DAP.Thread {
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
    scopes: ScopeNode[] = [];
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

export interface IScopeNode extends DAP.Scope {
    name: string;
    presentationHint?: string;
    variablesReference: number;
    frameId: number;

    path: string[];
    parentVariableReference: number;
    scopeType: ScopeType;
}

export class ScopeNode implements IScopeNode {
    readonly name: string;
    readonly presentationHint?: string;
    readonly variablesReference: number;
    readonly frameId: number;
    readonly parentVariableReference: number;

    readonly path: string[];
    readonly scopeType: ScopeType;
    readonly expensive: boolean;
    // implement getters and setters for path

    public getDAPScope(): DAP.Scope {
        return {
            name: this.name,
            presentationHint: this.presentationHint,
            variablesReference: this.variablesReference,
            expensive: this.expensive,
        };
    }
    public static ScopeFromVariable(varNode: VariableNode, stackFrame?: StackFrameNode, parentScope?: ScopeNode) {
        let _scopeType: ScopeType = 'objectMember';
        if (varNode.name.toLowerCase() == 'parent') {
            _scopeType = 'parent';
        } else if (varNode.isFakeReflectionVar) {
            _scopeType = 'reflectionItem';
        } else if (parentScope?.scopeType == 'localEnclosure') {
            _scopeType = 'localVar';
        }
        return new ScopeNode({
            name: varNode.name,
            variablesReference: varNode.variablesReference,
            frameId: stackFrame?.id || 0,
            path: [...(parentScope?.path || []), varNode.realName],
            scopeType: _scopeType,
            parentVariableReference: parentScope?.variablesReference || 0,
            expensive: false,
        } as IScopeNode);
    }

    public static ScopeFromStackFrame(
        stackFrame: StackFrameNode,
        varRef: number,
        makeLocal: boolean = false,
        parentVarRef = 0
    ) {
        const scope = {
            name: makeLocal ? 'Local' : 'Self',
            presentationHint: makeLocal ? 'locals' : undefined,
            variablesReference: varRef,
            expensive: false,
        } as DAP.Scope;

        if (scope.name == 'Self' && stackFrame.object) {
            scope.name = 'Self' + stackFrame.object;
        }
        return new ScopeNode({
            name: scope.name,
            presentationHint: scope.presentationHint,
            variablesReference: scope.variablesReference,
            frameId: stackFrame.id,
            path: scope.name == 'Local' ? [] : ['self'],
            parentVariableReference: parentVarRef,
            scopeType: scope.name == 'Local' ? 'localEnclosure' : 'self',
            expensive: scope.expensive,
        } as IScopeNode);
    }

    constructor(c: IScopeNode) {
        this.name = c.name;
        this.presentationHint = c.presentationHint;
        this.variablesReference = c.variablesReference;
        this.frameId = c.frameId;
        this.path = c.path;
        this.parentVariableReference = c.parentVariableReference;
        this.scopeType = c.scopeType;
        this.expensive = c.expensive;
    }
}

export interface IVariableNode extends DAP.Variable {
    name: string;
    value: string;
    type?: string;
    presentationHint?: DAP.VariablePresentationHint;
    evaluateName?: string;
    variablesReference: number;
    namedVariables?: number;
    indexedVariables?: number;
    memoryReference?: string;

    isProp: boolean;
    realName: string;
    compound: boolean;
}

export class VariableNode implements IVariableNode {
    readonly name: string;
    readonly value: string;
    readonly type: string;
    readonly presentationHint?: DAP.VariablePresentationHint;
    readonly variablesReference: number;
    readonly evaluateName?: string;
    readonly namedVariables?: number;
    readonly indexedVariables?: number;
    readonly memoryReference?: string;

    parentScope?: ScopeNode;
    readonly isProp: boolean;
    readonly realName: string;
    readonly compound: boolean;
    readonly isFakeReflectionVar: boolean;
    baseForm?: string;
    reflectionInfo?: SFDAP.Root;

    public static parseOutReflectionInfo(valueStr: string) {
        /**
         * Values for compound variables are returned like this:
         *
         * [{FormClass} <{reflection_data}>]
         *
         * the reflection_data is a short string that follows the following formats:
         *  form:
         *    %s (%08X)
         *    <nullptr form> (%08X)
         * //example: [Armor <Clothes_Miner_UtilitySuit (0001D1E7)>]
         *
         *  topicinfo - doesn't look like you can get this via the debugger
         *    topic info %08X on <nullptr quest>
         *    topic info %08X on quest %s (%08X)
         *
         *  alias:
         *    alias %s on quest %s (%08X)
         *    alias %s on <nullptr quest> (%08X)
         *    <nullptr alias> (%hu) on %squest %s (%08X)
         *    <nullptr alias> (%hu) on <nullptr quest> (%08X)
         *  example: [mq101playeraliasscript <alias Player on quest MQ101 (00003448)>]
         *
         *  inventoryItem:
         *    Item %hu in <nullptr container> (%08X)
         *    Item %hu in container %s (%08X)
         *  example: [Weapon <Item 21 in container Thing (00000014)>]
         *
         *  activeEffect:
         *    Active effect %hu on <nullptr actor> (%08X)
         *    Active effect %hu on %s (%08X)
         *  example: [MagicEffect <Active effect 1 on Actor (00005251)>]
         *
         *  inputEnableLayer:
         *    Input enable layer <no name> (%08X)
         *    Input enable layer %s (%08X)
         *    Invalid input enable layer (%08X)
         *  layerId is the formId of the layer `(XXXXXXXX)`
         *  example: [InputEvent <Input enable layer 1 on Player (00000007)>]
         *
         */
        /*                             v yes that space is supposed to be there */
        const re = /\[([\w\d_]+) <(.*)? \(([A-F\d]{8})\)>\]/g;
        const match = re.exec(valueStr);
        if (match?.length == 4) {
            const baseForm = match?.[1];
            const rInfo = match?.[2];
            const formId = parseInt(match?.[3], 16);

            if (!rInfo || rInfo.length == 0) {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'form',
                        formId: formId,
                    },
                };
            }
            if (rInfo.includes('alias') && rInfo.includes('on quest')) {
                const parts = rInfo.replace('alias ', '').split(' on quest ');
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'alias',
                        aliasName: parts[0],
                        questName: parts[1],
                        questFormId: formId,
                    },
                };
            } else if (rInfo.startsWith('Item ') && rInfo.includes(' in container ')) {
                const parts = rInfo.replace('Item ', '').split(' in container ');
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'inventoryItem',
                        uniqueId: parseInt(parts[0]),
                        containerName: parts[1],
                        containerFormId: formId,
                    },
                };
            } else if (rInfo.startsWith('Active effect ')) {
                const parts = rInfo.replace('Active effect ', '').split(' on ');
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'activeEffect',
                        effectId: parseInt(parts[0]),
                        targetName: parts[1],
                        targetFormId: formId,
                    },
                };
            } else if (rInfo.startsWith('Input enable layer ')) {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'inputEnableLayer',
                        layerId: formId,
                    },
                };
            } else if (!rInfo.includes(' ')) {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'form',
                        formId: formId,
                        formName: rInfo,
                    },
                };
            } else {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'form',
                        formId: formId,
                    },
                };
            }
        }
        return undefined;
    }

    public getEvaluateName() {
        if (!this.parentScope || this.parentScope.scopeType == 'localEnclosure' || this.parentScope.path.length == 0) {
            return [this.realName];
        }
        return [...this.parentScope.path, this.realName];
    }
    public getDAPVariable(): DAP.Variable {
        return {
            name: this.name,
            value: this.value,
            type: this.type,
            presentationHint: this.presentationHint,
            evaluateName: this.evaluateName,
            variablesReference: this.variablesReference,
            namedVariables: this.namedVariables,
            indexedVariables: this.indexedVariables,
            memoryReference: this.memoryReference,
        };
    }

    constructor(variable: SFDAP.Variable, varRef: number, isFakeReflectionVar: boolean, parentScope?: ScopeNode) {
        this.name = variable.name;
        this.value = variable.value;
        this.type = variable.type;
        this.variablesReference = variable.compound ? varRef : 0;
        this.realName = variable.name;
        this.compound = variable.compound;
        this.parentScope = parentScope;
        this.isFakeReflectionVar = isFakeReflectionVar;
        if (isFakeReflectionVar) {
            const refinfo = VariableNode.parseOutReflectionInfo(variable.name);
            if (refinfo) {
                this.reflectionInfo = refinfo.root as SFDAP.Root;
                this.baseForm = refinfo.baseForm;
            }
        } else if (variable.compound) {
            const reflectionInfo = VariableNode.parseOutReflectionInfo(variable.value);
            if (reflectionInfo) {
                this.reflectionInfo = reflectionInfo.root as SFDAP.Root;
                this.baseForm = reflectionInfo.baseForm;
            }
        }
        if (variable.name.startsWith('::') && variable.name.endsWith('_var')) {
            this.name = variable.name.substring(2, variable.name.length - 4);
            this.realName = this.name;
            this.presentationHint = {
                kind: 'property',
            };
            this.isProp = true;
        }
        this.isProp = false;
    }
}

export interface IStateNode {
    readonly threads: IThreadNode[];
}

export class StackFrameMap extends Map<number, StackFrameNode> {}
export class ScopeMap extends Map<number, ScopeNode> {}

export class VariableMap extends Map<number, VariableNode> {}

export class StackFrameArray extends Array<StackFrameNode> {
    // set the members of the array to be constant
    readonly [index: number]: StackFrameNode;
}

export class StateNode implements IStateNode {
    private readonly DUMMY_THREAD_NAME = 'DUMMY THREAD';
    private readonly DUMMY_THREAD_ID = 0;
    private readonly DUMMY_THREAD_OBJ: ThreadNode = new ThreadNode({
        id: this.DUMMY_THREAD_ID,
        name: this.DUMMY_THREAD_NAME,
    } as SFDAP.Thread);
    public get threads(): ThreadNode[] {
        return this._threads;
    }
    public get stackFrames(): DAP.StackFrame[] {
        return StackFrameArray.from(this._stackFrameMap.values()).map((v) => v.getDAPStackFrame());
    }
    private _threads: ThreadNode[] = [this.DUMMY_THREAD_OBJ];
    private _threadMap: Map<number, ThreadNode> = new Map<number, ThreadNode>();
    private _stackFrameMap: StackFrameMap = new StackFrameMap();
    private _stackIdToThreadIdMap: Map<number, number> = new Map<number, number>();
    private _scopeMap: ScopeMap = new ScopeMap();
    private _variableMap: VariableMap = new VariableMap();
    private _variableReferencetoFrameIdMap: Map<number, number> = new Map<number, number>();
    private _variableRefCount = 0;
    private _localScopeVarRefToSelfScopeVarRefMap: Map<number, number> = new Map<number, number>();
    constructor() {}

    public getVariableRefCount() {
        this._variableRefCount++;
        return this._variableRefCount;
    }

    public hasThread(threadId: number): boolean {
        return this._threadMap.has(threadId);
    }
    public getThread(threadId: number): DAP.Thread {
        return this.getThreadNode(threadId).DAPThread;
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

    protected getScopeNode(varRef: number): ScopeNode {
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

    public hasVariable(varRef: number): boolean {
        return this._variableMap.has(varRef);
    }

    public getVariable(varRef: number): DAP.Variable {
        return this.getVariableNode(varRef).getDAPVariable();
    }

    protected getVariableNode(varRef: number): VariableNode {
        if (varRef > this._variableRefCount || !this.hasVariable(varRef)) {
            throw new Error(`Variable reference ${varRef} is out of range`);
        }
        return this._variableMap.get(varRef)!;
    }

    protected addScopeToScopeMap(scope: ScopeNode) {
        this._scopeMap.set(scope.variablesReference, scope);
        this._variableReferencetoFrameIdMap.set(scope.variablesReference, scope.frameId);
    }

    protected makeScopeNodesForStackFrame(frameId: number): ScopeNode[] | undefined {
        const frame = this._stackFrameMap.get(frameId)!;
        const localScope = ScopeNode.ScopeFromStackFrame(frame, this.getVariableRefCount(), true);
        const selfScope = ScopeNode.ScopeFromStackFrame(
            frame,
            this.getVariableRefCount(),
            false,
            localScope.variablesReference
        );
        const scopes = [localScope, selfScope];
        this.addScopeToScopeMap(localScope);
        this.addScopeToScopeMap(selfScope);
        this._localScopeVarRefToSelfScopeVarRefMap.set(localScope.variablesReference, selfScope.variablesReference);
        return scopes;
    }
    public makeScopesForStackFrame(frameId: number): ScopeNode[] | undefined {
        if (this.hasStackFrame(frameId)) {
            return this.makeScopeNodesForStackFrame(frameId);
        }
        throw new Error(`Stack frame ${frameId} not found`);
    }
    protected getSelfScopeRef(frameid: number): number | undefined {
        if (this._stackFrameMap.has(frameid)) {
            const frame = this._stackFrameMap.get(frameid)!;
            const selfScope = frame.scopes.find((s) => s.name == 'Self');
            return selfScope?.variablesReference || undefined;
        }
        return undefined;
    }

    getScopesForVariable(varRef: number): ScopeNode {
        return this._scopeMap.get(varRef)!;
    }

    addVariablesToState(variables: SFDAP.Variable[], parentScopeVarRef: number, frameid?: number): VariableNode[] {
        const newVariables = [];
        if (frameid && !this._stackFrameMap.has(frameid)) {
            throw new Error(`Stack frame ${frameid} not found`);
        }
        if (!this._scopeMap.has(parentScopeVarRef)) {
            throw new Error(`Parent scope ${parentScopeVarRef} not found`);
        }

        const parentScope = this._scopeMap.get(parentScopeVarRef);

        for (const oldVar of variables) {
            const varNode = new VariableNode(oldVar, this.getVariableRefCount(), false, parentScope);
            if (parentScope && oldVar.compound) {
                // careful about "self"
                if (
                    oldVar.name.toLowerCase() == 'self' &&
                    parentScope.scopeType === 'localEnclosure' &&
                    parentScope.path
                ) {
                    const selfVarRef = this.getSelfScopeRef(parentScope.variablesReference);
                    if (selfVarRef) {
                        if (this._scopeMap.has(selfVarRef)) {
                            if (!this._variableMap.has(selfVarRef)) {
                                this._variableMap.set(selfVarRef, varNode);
                            }
                            // no need to make a new scope, just use the old one; we won't put this in the locals variables returned to the client
                            continue;
                        } else {
                            // this.logwarn('Could not find self scope for variable reference: ' + selfVarRef);
                        }
                    }
                }
                const stackFrame = frameid ? this._stackFrameMap.get(frameid)! : undefined;

                const ScopeFromVariable: ScopeNode = ScopeNode.ScopeFromVariable(varNode, stackFrame, parentScope);
                this._scopeMap.set(varNode.variablesReference, ScopeFromVariable);
                this._variableMap.set(varNode.variablesReference, varNode);
            }
            newVariables.push(varNode);
        }
        return newVariables;
    }
}
