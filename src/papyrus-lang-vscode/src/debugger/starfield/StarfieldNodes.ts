import { DebugProtocol as DAP } from "@vscode/debugprotocol";
import { StarfieldDebugProtocol as SFDAP } from "./StarfieldDebugProtocol";
export interface StackFrameNode extends DAP.StackFrame {
    id: number;
    name: string;
    source: DAP.Source;
    object: string;
    line: number;
    column: number;
    threadId: number;
}


export interface ScopeNode extends DAP.Scope{
    name: string;
    presentationHint?: string;
    variablesReference: number;
    threadId: number;
    frameId: number;
    path: string[];
    parentVariableReference: number;
    scopeType: "local" | "self" | "parent" | "objectMember";
    objectName?: string;
    propName?: string;
    baseForm?: string;
    reflectionInfo?: SFDAP.Root;
}
