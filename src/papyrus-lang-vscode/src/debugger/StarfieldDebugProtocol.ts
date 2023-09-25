import { SourceBreakpoint } from "vscode";
import { DebugProtocol as DAP } from "@vscode/debugprotocol";

// Starfield breaks the DAP spec in ways that are not easily fixable, so we have to implement our own proxy
// custom Output Event
export declare module StarfieldDebugProtocol {

type ProtocolMessage = DAP.ProtocolMessage;
type Request = DAP.Request;
type Response = DAP.Response;
type Event = DAP.Event;

interface OutputEvent extends DAP.Event {
    //event: 'output';
    body: {
        channel: string; // no idea what this is
        isError: boolean;
        output: string;
        severity: 'info'|'warning'|'error'|'fatal'|'invalid severity'
    };
}
// New "Version" event, Doesn't exist in DAP spec
interface VersionEvent extends DAP.Event {
    //event: 'version';
    body: {
        game: string; // always "Starfield"
        version: number;
    };
}

interface Root {
    type: string; // "stackFrame" | "value"
    threadId?: number;
    stackFrameIndex?: number;
    valueType?: string;
}

interface VariablesArguments {
    root: Root;
    path: string[]; // no idea how this is used
}

interface VariablesRequest extends DAP.Request {
    //command: "variables",
    arguments: VariablesArguments;
}


interface Variable {
    name: string;
    value: string;
    type: string;
    compound: boolean;
}

interface VariablesResponse extends DAP.Response {
    body: {
        variables: Variable[];
    };
}

// Doesn't exist in DAP spec, don't know what this does
interface ValueRequest extends DAP.Request {
    //command: "value", // Doesn't exist in DAP spec
    arguments: VariablesArguments; // uses same args as variables
}

interface ValueResponse extends DAP.Response {
    body: {
        value: string;
        type: string;
        compound: boolean;
    };
}

// Custom StackFrame response
interface StackFrame {
    name: string; // name of the stack frame
    object: string; // No idea what this is
    source?: string; // NOTE: This is a string, not a Source object; it's the namespaced object name (e.g. "MyMod:MyScript")
    line?: number; // line it's currently at
}

// StackTraceRequest is the same
type StackTraceRequest = DAP.StackTraceRequest;

interface StackTraceResponse extends DAP.Response {
    body: {
        stackFrames: StackFrame[];
    };
}


// custom set breakpoints handling
interface SetBreakpointsArguments {
    /** string instead of a `Source` object; it's the namespaced object name (e.g. "MyMod:MyScript")  */
    source: string;
    /** The code locations of the breakpoints. (Starfield only reads the `line` field from this) */
    breakpoints?: DAP.SourceBreakpoint[];
}
interface SetBreakpointsRequest extends DAP.Request {
    //command: "setBreakpoints",
    arguments: SetBreakpointsArguments;
}

interface CustomBreakpoint {
    /** string instead of a `Source` object; it's the namespaced object name (e.g. "MyMod:MyScript")  */
    source: string;
    line: number;
    verified: boolean;
}

interface SetBreakpointsResponse extends DAP.Response {
    body: {
        breakpoints: CustomBreakpoint[];
    };
}
}