import { DebugProtocol as DAP } from "@vscode/debugprotocol";
import { StarfieldDebugProtocol as SFDAP } from "./StarfieldDebugProtocol";
export interface IStackFrameNode extends DAP.StackFrame {
    id: number;
    name: string;
    source?: DAP.Source;
    line: number;
    column: number;
    object: string;
    threadId: number;
    realStackIndex: number;
}
export class StackFrameNode implements IStackFrameNode{
    readonly id: number;
    readonly name: string;
    readonly source?: DAP.Source;
    readonly line: number;
    readonly column: number;
    endLine?: number;
    endColumn?: number;
    canRestart?: boolean;
    instructionPointerReference?: string;
    readonly moduleId?: number | string;
    presentationHint?: 'normal' | 'label' | 'subtle';
    readonly object: string;
    readonly threadId: number;
    readonly realStackIndex: number;
    private GetUniqueStackFrameId(threadId: number, stackindex: number){
        return threadId * 1000 + stackindex;
    }
    public getFunctionInfo(){
        // stackframe name is in the format of `objectname.statename.function(...)`
        // if it's the global state, it's `objectname..function(...)`; two dots indicate blank state name
        let parts = this.name.replace("(...)","").split(".")
        if (parts.length < 3) {
            return {
                objectName: "",
                stateName: "",
                functionName: ""
            };
        }
        let functionName = parts[2];
        if (functionName.startsWith("::remote_")){
            functionName = functionName.substring(8);
            if (functionName.indexOf("_")){
                let idx = functionName.indexOf("_");
                // replace the first `_` with '.' using slice
                // No base forms with events have a `_` in them, so this is safe
                functionName = functionName.slice(0, idx) + "." + functionName.slice(idx + 1);
            }
        }
        return {
            objectName: parts[0],
            stateName: parts[1],
            functionName: parts[2]
        }
    }
    public getStandardDAPFrame(lineStartsAt1: boolean = true, ColumnStartsAt1: boolean = true): DAP.StackFrame{
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
            presentationHint: this.presentationHint
        }
    }
    /**
     * @param frame The stack frame to create a node for
     * @param threadId The thread id of the stack frame
     * @param stackIndex The index of the stack frame in the thread (i.e. top is 0, next is 1, etc.)
     */
    constructor(frame: SFDAP.StackFrame, threadId: number, stackIndex: number, source?: DAP.Source){
        this.id = this.GetUniqueStackFrameId(threadId, stackIndex)
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

export type ScopeType = "localEnclosure" | "localVar" | "self" | "parent" | "objectMember" | "reflectionItem";

export interface IScopeNode extends DAP.Scope{
    name: string;
    presentationHint?: string;
    variablesReference: number;
    threadId: number;
    frameId: number;

    path: string[];
    parentVariableReference: number;
    scopeType: ScopeType;
}

export class ScopeNode implements IScopeNode{
    readonly name: string;
    readonly presentationHint?: string;
    readonly variablesReference: number;
    readonly threadId: number;
    readonly frameId: number;
    readonly parentVariableReference: number;

    path: string[];
    scopeType: ScopeType;
    readonly expensive: boolean;


    public getDAPScope(): DAP.Scope{
        return {
            name: this.name,
            presentationHint: this.presentationHint,
            variablesReference: this.variablesReference,
            expensive: this.expensive
        }
    }
    public static ScopeFromVariable(varNode: VariableNode, stackFrame: StackFrameNode, parentScope?: ScopeNode){
        let _scopeType: ScopeType = "objectMember";
        if (varNode.name.toLowerCase() == "parent"){
            _scopeType = "parent";
        } else if (varNode.isFakeReflectionVar){
            _scopeType = "reflectionItem";
        } else if (parentScope?.scopeType == "localEnclosure"){
            _scopeType = "localVar";
        }
        return new ScopeNode({
            name: varNode.name,
            variablesReference: varNode.variablesReference,
            threadId: stackFrame.threadId,
            frameId: stackFrame.id,
            path: [...parentScope?.path || [], varNode.realName],
            scopeType: _scopeType,
            parentVariableReference: parentScope?.variablesReference || 0,
            expensive: false,
        } as IScopeNode);
    }

    public static ScopeFromStackFrame(stackFrame: StackFrameNode, varRef: number, makeLocal: boolean = false, parentVarRef = 0){
        let scope = {
            name: makeLocal ? "Local" : "Self",
            presentationHint: makeLocal ? "locals" : undefined,
            variablesReference: varRef,
            expensive: false
        } as DAP.Scope;

        if (scope.name == "Self" && stackFrame.object){
            scope.name = "Self" + stackFrame.object;
        }
        return new ScopeNode ({
            name : scope.name,
            presentationHint : scope.presentationHint,
            variablesReference : scope.variablesReference,
            threadId : stackFrame.threadId,
            frameId : stackFrame.id,
            path: scope.name == "Local" ? [] : ["self"],
            parentVariableReference : parentVarRef,
            scopeType : scope.name == "Local" ? "localEnclosure" : "self",
            expensive : scope.expensive
        } as IScopeNode)
    }

    constructor(c: IScopeNode){
        this.name = c.name;
        this.presentationHint = c.presentationHint;
        this.variablesReference = c.variablesReference;
        this.threadId = c.threadId;
        this.frameId = c.frameId;
        this.path = c.path;
        this.parentVariableReference = c.parentVariableReference;
        this.scopeType = c.scopeType;
        this.expensive = c.expensive;
    }
}

export interface IVariableNode extends DAP.Variable{
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

export class VariableNode implements IVariableNode{
        readonly name: string;
        readonly value: string;
        readonly type: string;
        presentationHint?: DAP.VariablePresentationHint;
        readonly variablesReference: number;
        evaluateName?: string;
        namedVariables?: number;
        indexedVariables?: number;
        memoryReference?: string;
        
        parentScope?: ScopeNode;
        readonly isProp: boolean;
        readonly realName: string;
        readonly compound: boolean;
        readonly isFakeReflectionVar: boolean;
        baseForm?: string;
        reflectionInfo?: SFDAP.Root;

    public static parseOutReflectionInfo(valueStr: string){
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
         *  example: [InputEvent <Input enable layer 1 on Player (00000007)>]
         *  
         */

        let valueTypes = {};
        let value = valueStr;      //v yes that space is supposed to be there
        let re = /\[([\w\d_]+) <(.*)? \(([A-F\d]{8})\)>\]/g;
        let match = re.exec(valueStr);
        if (match?.length == 4){
            let baseForm = match?.[1];
            let reflectionInfo = match?.[2];
            let formId = parseInt(match?.[3], 16);
            

            if (!reflectionInfo || reflectionInfo.length == 0){
                return {
                    baseForm: baseForm,
                    root: {
                        type: "value",
                        valueType: "form",
                        formId: formId
                    }
                }
            }
            if (reflectionInfo.includes("alias") && reflectionInfo.includes("on quest")){
                let parts = reflectionInfo.replace("alias ", "").split(" on quest ");
                return {
                    baseForm: baseForm,
                    root: {
                        type: "value",
                        valueType: "alias",
                        aliasName: parts[0],
                        questName: parts[1],
                        questFormId: formId
                    }
                }
            } else if (reflectionInfo.startsWith("Item ") && reflectionInfo.includes(" in container ")){
                let parts = reflectionInfo.replace("Item ", "").split(" in container ");
                return {
                    baseForm: baseForm,
                    root: {
                        type: "value",
                        valueType: "inventoryItem",
                        uniqueId: parseInt(parts[0]),
                        containerName: parts[1],
                        containerFormId: formId
                    }
                }
            } else if (reflectionInfo.startsWith("Active effect ")){
                let parts = reflectionInfo.replace("Active effect ", "").split(" on ");
                return {
                    baseForm: baseForm,
                    root: {
                        type: "value",
                        valueType: "activeEffect",
                        effectId: parseInt(parts[0]),
                        targetName: parts[1],
                        targetFormId: formId
                    }
                }
            } else if (reflectionInfo.startsWith("Input enable layer ")){
                let parts = reflectionInfo.replace("Input enable layer ", "").split(" on ");
                return {
                    baseForm: baseForm,
                    root: {
                        type: "value",
                        valueType: "inputEnableLayer",
                        layerId: formId,
                    }
                }
            } else if (!reflectionInfo.includes(" ")){
                return {
                    baseForm: baseForm,
                    root: {
                        type: "value",
                        valueType: "form",
                        formId: formId,
                        formName: reflectionInfo
                    }
                }
            } else {
                return {
                    baseForm: baseForm,
                    root: {
                        type: "value",
                        valueType: "form",
                        formId: formId
                    }
                }
            }
        }
        return undefined;        
    }

    public getEvaluateName(){
        if (!this.parentScope || this.parentScope.scopeType == "localEnclosure" || this.parentScope.path.length == 0){
            return [this.realName];
        }
        return [...this.parentScope.path, this.realName];
    }
    public getDAPVariable(): DAP.Variable{
        return {
            name: this.name,
            value: this.value,
            type: this.type,
            presentationHint: this.presentationHint,
            evaluateName: this.evaluateName,
            variablesReference: this.variablesReference,
            namedVariables: this.namedVariables,
            indexedVariables: this.indexedVariables,
            memoryReference: this.memoryReference
        }
    }

    constructor(variable: SFDAP.Variable, varRef: number, isFakeReflectionVar: boolean, parentScope?: ScopeNode){
        this.name = variable.name;
        this.value = variable.value;
        this.type = variable.type;
        this.variablesReference = variable.compound ? varRef : 0;
        this.realName = variable.name;
        this.compound = variable.compound;
        this.parentScope = parentScope;
        this.isFakeReflectionVar = isFakeReflectionVar;
        if (isFakeReflectionVar){
            let refinfo = VariableNode.parseOutReflectionInfo(variable.name)
            if (refinfo){
                this.reflectionInfo = refinfo.root as SFDAP.Root;
                this.baseForm = refinfo.baseForm;
            }
        }
        else if (variable.compound){
            let reflectionInfo = VariableNode.parseOutReflectionInfo(variable.value);
            if (reflectionInfo){
                this.reflectionInfo = reflectionInfo.root as SFDAP.Root;
                this.baseForm = reflectionInfo.baseForm;
            }
        }
        if (variable.name.startsWith("::") && variable.name.endsWith("_var")){
            this.name = variable.name.substring(2, variable.name.length - 4);
            this.realName = this.name;
            this.presentationHint = {
                kind: "property"
            }
            this.isProp = true;
        }
        this.isProp = false;
    }
}