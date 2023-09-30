import { DebugProtocol as DAP } from "@vscode/debugprotocol";
import * as fs from 'fs'
import * as path from 'path'
import { StarfieldDebugProtocol as SFDAP } from "./StarfieldDebugProtocol";
import { DebugAdapterProxy, DebugAdapterProxyOptions } from "./DebugAdapterProxy";
import { Response, Event, Message } from "@vscode/debugadapter/lib/messages";
import { ScopeNode } from "./StarfieldNodes";
import * as url from "url";


export enum ErrorDestination {
	User = 1,
	Telemetry = 2
};

class Request extends Message implements DAP.Request {
    command: string;
    arguments?: any;
    constructor(command: string, args?: any) {
        super("request");
        this.command = command;
        this.arguments = args;
    }
}

export interface StarfieldDebugAdapterProxyOptions extends DebugAdapterProxyOptions {
    workspaceFolder: string;
    BaseScriptFolder?: string;
}

export class StarfieldDebugAdapterProxy extends DebugAdapterProxy {
    private readonly DUMMY_THREAD_NAME = "DUMMY THREAD";
    private readonly DUMMY_THREAD_OBJ: DAP.Thread = {
        id: 0,
        name: this.DUMMY_THREAD_NAME
    }

    private _pendingRequests = new Map<number, (response: DAP.Response) => void>();
    private workspaceFolder :string = "";
    private BaseScriptFolder :string | undefined = undefined;
    private receivedVersionEvent: boolean = false;
    private receivedLaunchOrAttachRequest: boolean = false;
    private sentInitializedEvent: boolean = false;
    // object name to source map
    protected objectNameToSourceMap: Map<string, DAP.Source> = new Map<string, DAP.Source>();
    protected pathtoObjectNameMap: Map<string, string> = new Map<string, string>();
    private _threads: DAP.Thread[] = [this.DUMMY_THREAD_OBJ];
    private _stackFrameMap: Map<number, DAP.StackFrame> = new Map<number, DAP.StackFrame>();
    private _stackIdToThreadIdMap: Map<number, number> = new Map<number, number>();
    private _scopeMap: Map<number, ScopeNode> = new Map<number, any>();
    private _variableMap: Map<number, DAP.Variable> = new Map<number, DAP.Variable>();
    private _variableReferencetoFrameIdMap: Map<number, number> = new Map<number, number>();
    private _variableRefCount = 0;
    private currentSeq: number = 0;
    private _debuggerPathsAreURIs = false;
    private _clientPathsAreURIs: boolean = false;
    private _debuggerColumnsStartAt1 = true;
    private _clientColumnsStartAt1: boolean = true;
    private _debuggerLinesStartAt1 = true;
    private _clientLinesStartAt1: boolean = true;
    constructor(options: StarfieldDebugAdapterProxyOptions) {
        const logdir = path.join(process.env.USERPROFILE || process.env.HOME || ".", "Documents", "My Games", "Starfield", "Logs");
        options.logdir = options.logdir || logdir;
        super(options);
        this.logClientToProxy = "trace"
        this.logProxyToServer = "info"
        this.logServerToProxy = "info"
        this.logProxyToClient = "info"
    }

    getVariableRefCount(){
        this._variableRefCount++;
        return this._variableRefCount;
    }
    clearExecutionState(){
        this._variableRefCount = 0;
        this._stackFrameMap.clear();
        this._scopeMap.clear();
        this._variableMap.clear();
        this._variableReferencetoFrameIdMap.clear();
        this._stackIdToThreadIdMap.clear();
    }
    handlePauseButton(){

    }
    // takes in a Source object and returns the papyrus object idnetifier (e.g. "MyMod:MyScript")
    sourceToObjectName(source: DAP.Source) {
        let name = source.name || "";
        let path = source.path || "";

        let objectName: string = name.split(".")[0];

        // check the object name map path first
        if (this.pathtoObjectNameMap.has(path)) {
            objectName = this.pathtoObjectNameMap.get(path)!;
            this.objectNameToSourceMap.set(objectName, source);
        } else if (path) {
            let newName = this.GetObjectNameFromScript(path);
            if (!newName) {
                this.logerror("Did not find script name in file: " + path);
            } else {
                objectName = newName;
                this.pathtoObjectNameMap.set(path, objectName);
                this.objectNameToSourceMap.set(objectName, source);
            }
            // set the object name map path
        } else {
            // last ditch; if objectName is in source
            if (this.objectNameToSourceMap.has(objectName)){
                this.objectNameToSourceMap.set(objectName, source);
            }            
        }
        return objectName;
    }


    //overrides base class
    handleMessageFromServer(message: DAP.ProtocolMessage): void {
        if (message.type == "response") {
            const response = <DAP.Response>message;
			const clb = this._pendingRequests.get(response.request_seq);
            
            // The callbacks should handle all the responses we need to translate into the expected response objects,
            // but just in case Starfield screws up the request_seq number, we handle them below
			if (clb) {
				this._pendingRequests.delete(response.request_seq);
				clb(response);
                return;
			}
            this.sendMessageToClient(response);
        } else if (message.type == "event") {
            const event = message as DAP.Event;
            if (event.event == "output") {
                this.handleOutputEvent(event as SFDAP.OutputEvent);
            } else if (event.event == "version") {
                this.handleVersionEvent(event as SFDAP.VersionEvent);
            } else if (event.event == "thread") {
                this.handleThreadEvent(event as SFDAP.ThreadEvent);
            } else if (event.event == "stopped") {
                this.handleStoppedEvent(event as SFDAP.StoppedEvent);
            } else {
                this.sendMessageToClient(event);
            }
        } else {
            this.sendMessageToClient(message);
        }
    }    

    protected handleStoppedEvent(message: DAP.StoppedEvent) {
        // TODO: handle stopped events
        this.sendMessageToClient(message);
    }
    protected handleThreadEvent(message: SFDAP.ThreadEvent): void {
        if (message.body.reason == "started") {
            // check for existence of dummy thread
            if (this._threads.length == 1 && this._threads[0].name == this.DUMMY_THREAD_NAME) {
                this._threads.pop();
            }
            this._threads.push({
                id: message.body.threadId,
                name: "<thread " + message.body.threadId + ">"
            });
        } else if (message.body.reason == "exited") {
            // remove thread from list
            this._threads = this._threads.filter((thread) => {
                return thread.id != message.body.threadId;
            });
            // if no threads left, add dummy thread
            if (this._threads.length == 0) {
                this._threads.push(this.DUMMY_THREAD_OBJ);
            }
        }
        this.sendMessageToClient(message);
    }

    //overrides base class
    protected handleMessageFromClient(message: DAP.ProtocolMessage): void {
        let pmessage = message as DAP.ProtocolMessage
        let retries = 0;
        this.currentSeq = message.seq;
        if (pmessage.type === "request") {
            this.handleClientRequest(pmessage as DAP.Request);
        } else {
            // TODO: handle other message types
            this.sendMessageToServer(pmessage);
        }
        
    }
    
    handleVersionEvent(message: SFDAP.VersionEvent) {
        this.receivedVersionEvent = true;
        this.maybeSendFakeInitializedEvent();
    }

    maybeSendFakeInitializedEvent() {
        if (this.receivedVersionEvent && this.receivedLaunchOrAttachRequest && !this.sentInitializedEvent) {
            this.loginfo("***PROXY->CLIENT - SENDING FAKE initialized event")
            this.sendMessageToClient(new Event('initialized'))
            this.sentInitializedEvent = true;
        }
    }
    
    handleOutputEvent(message: SFDAP.OutputEvent) {
        // The output messages don't have newlines, so just append one.
        // TODO: something with the rest of the fields?
        message.body.output += "\n";
        this.sendMessageToClient(message);
    }


    private static _formatPIIRegexp = /{([^}]+)}/g;

	private static formatPII(format:string, excludePII: boolean, args: {[key: string]: string}): string {
		return format.replace(StarfieldDebugAdapterProxy._formatPIIRegexp, function(match, paramName) {
			if (excludePII && paramName.length > 0 && paramName[0] !== '_') {
				return match;
			}
			return args[paramName] && args.hasOwnProperty(paramName) ?
				args[paramName] :
				match;
		})
	}


	protected sendErrorResponse(response: DAP.Response, codeOrMessage: number | DAP.Message, format?: string, variables?: any, dest: ErrorDestination = ErrorDestination.User): void {
		let msg : DAP.Message;
		if (typeof codeOrMessage === 'number') {
			msg = <DAP.Message> {
				id: <number> codeOrMessage,
				format: format
			};
			if (variables) {
				msg.variables = variables;
			}
			if (dest & ErrorDestination.User) {
				msg.showUser = true;
			}
			if (dest & ErrorDestination.Telemetry) {
				msg.sendTelemetry = true;
			}
		} else {
			msg = codeOrMessage;
		}

		response.success = false;
		response.message = StarfieldDebugAdapterProxy.formatPII(msg.format, true, msg.variables || {});
		if (!response.body) {
			response.body = { };
		}
		response.body.error = msg;

		this.sendMessageToClient(response);
	}

    public sendRequestToServerWithCB(request: SFDAP.Request, timeout: number, cb: (response: SFDAP.Response) => void, nolog: boolean = false) : void {
        this.sendMessageToServer(request, nolog);
		if (cb) {
			this._pendingRequests.set(request.seq, cb);
            if (timeout > 0) {
                const timer = setTimeout(() => {
                    clearTimeout(timer);
                    const clb = this._pendingRequests.get(request.seq);
                    if (clb) {
                        this._pendingRequests.delete(request.seq);
                        clb(new Response(request, 'timeout'));
                    }
                }, timeout);
            }
		}
	}
    

	public sendRunInTerminalRequest(args: DAP.RunInTerminalRequestArguments, timeout: number, cb: (response: DAP.RunInTerminalResponse) => void) {
        let request = <DAP.RunInTerminalRequest> new Message("request");
        request.arguments = args;
		this.sendRequestToServerWithCB(request, timeout, (r: DAP.Response) => {
            r.command = "runInTerminal";
        });
	}

	protected handleClientRequest(request: DAP.Request): void {
		try {
			if (request.command === 'initialize') {
                this.handleInitializeRequest(<DAP.InitializeRequest> request);
			} else if (request.command === 'launch') {
				this.handleLaunchRequest(<DAP.LaunchRequest> request);

			} else if (request.command === 'attach') {
				this.handleAttachRequest(<DAP.AttachRequest> request);

			} else if (request.command === 'disconnect') {
				this.handleDisconnectRequest(<DAP.DisconnectRequest> request);

			} else if (request.command === 'setBreakpoints') {
				this.handleSetBreakpointsRequest(<DAP.SetBreakpointsRequest> request);


			} else if (request.command === 'continue') {
				this.handleContinueRequest(<DAP.ContinueRequest> request);

			} else if (request.command === 'next') {
				this.handleNextRequest(<DAP.NextRequest> request);

			} else if (request.command === 'stepIn') {
				this.handleStepInRequest(<DAP.StepInRequest> request);

			} else if (request.command === 'stepOut') {
				this.handleStepOutRequest(<DAP.StepOutRequest> request);

			} else if (request.command === 'pause') {
				this.handlePauseRequest(<DAP.PauseRequest> request);

			} else if (request.command === 'stackTrace') {
				this.handleStackTraceRequest(<DAP.StackTraceRequest> request);

			} else if (request.command === 'scopes') {
				this.handleScopesRequest(<DAP.ScopesRequest> request);

			} else if (request.command === 'variables') {
				this.handleVariablesRequest(<DAP.VariablesRequest> request);

			} else if (request.command === 'source') {
				this.handleSourceRequest(<DAP.SourceRequest> request);

			} else if (request.command === 'threads') {
				this.handleThreadsRequest(<DAP.ThreadsRequest> request);
            } else if (request.command === 'value'){
                this.handleValueRequest(<SFDAP.ValueRequest> request);
            } else if (request.command === 'evaluate') {
                this.handleEvaluateRequest(<DAP.EvaluateRequest> request);
			} else {
				this.handleCustomRequest(request);
			}
		} catch (e) {
			this.sendErrorResponse(new Response(request), 1104, '{_stack}', e, ErrorDestination.Telemetry);
		}
	}

    handleValueRequest(request: SFDAP.ValueRequest) {
        // This came from the REPL console, just send it to the server
        this.sendRequestToServerWithCB(request, 10000, (r: DAP.Response) =>{
            this.loginfo({message:r}, `!!!SERVER->PROXY - Received value response - path: ${request.arguments.path}!!!`);
        });
    }
        
    // Using this for debugging/RE; in the debug console of the client, you can type in server requests and they will be sent to the server
    handleEvaluateRequest(request: DAP.EvaluateRequest) {
        try{
            if (request.arguments.context == "repl") {
                let message = JSON.parse(request.arguments.expression)
                //check that it's a valid DAP.ProtocolMessage
                // make sure we have a valid sequence number
                message.seq = 10000 + this.currentSeq++;
                if (message.type && message.seq) {
                    // send it to the server
                    this.loginfo("!!!PROXY->SERVER - Sending message from REPL console to server!!!");
                    if (message.type == "request" && message.command) {

                        // special handler for variableRequest
                        if (message.command == "variables" && message?.arguments?.hasOwnProperty("root") && message?.arguments?.hasOwnProperty("path")) {
                            // formatted correctly, send it to the server
                            this.sendMessageToServer(message as DAP.ProtocolMessage);
                        } else {
                            this.handleClientRequest(message);
                        }
                    } else {
                        this.sendMessageToServer(message as DAP.ProtocolMessage);
                    }
                } else {
                    this.sendErrorResponse(new Response(request), 1104, "Invalid REPL message!", null, ErrorDestination.User);
                }
            } else {
                this.sendErrorResponse(new Response(request), 1104, "Invalid Evaluate request!!", null, ErrorDestination.User);
            }
        } catch (e) {
            this.sendErrorResponse(new Response(request), 1104, 'Invalid expression in REPL message!', e, ErrorDestination.Telemetry);
        }
    }

	protected handleInitializeRequest(request: DAP.InitializeRequest) : void {
        let args = request.arguments;
        if (typeof args.linesStartAt1 === 'boolean') {
            this._clientLinesStartAt1 = args.linesStartAt1;
        }
        if (typeof args.columnsStartAt1 === 'boolean') {
            this._clientColumnsStartAt1 = args.columnsStartAt1;
        }
        if (typeof args.pathFormat === 'string') {
            this._clientPathsAreURIs = args.pathFormat === 'uri';
        }


        let response = <DAP.InitializeResponse> new Response(request);
        response.body = response.body || {};

        // Starfield supports NONE OF THESE

		response.body.supportsConditionalBreakpoints = false;
		response.body.supportsHitConditionalBreakpoints = false;
		response.body.supportsFunctionBreakpoints = false;
		response.body.supportsConfigurationDoneRequest = false;
		response.body.supportsEvaluateForHovers = false;
		response.body.supportsStepBack = false;
		response.body.supportsSetVariable = false;
		response.body.supportsRestartFrame = false;
		response.body.supportsStepInTargetsRequest = false;
		response.body.supportsGotoTargetsRequest = false;
		response.body.supportsCompletionsRequest = false;
		response.body.supportsRestartRequest = false;
		response.body.supportsExceptionOptions = false;
		response.body.supportsValueFormattingOptions = false;
		response.body.supportsExceptionInfoRequest = false;
		response.body.supportTerminateDebuggee = false;
		response.body.supportsDelayedStackTraceLoading = false;
		response.body.supportsLoadedSourcesRequest = false;
		response.body.supportsLogPoints = false;
		response.body.supportsTerminateThreadsRequest = false;
		response.body.supportsSetExpression = false;
		response.body.supportsTerminateRequest = false;
		response.body.supportsDataBreakpoints = false;
		response.body.supportsReadMemoryRequest = false;
		response.body.supportsDisassembleRequest = false;
		response.body.supportsCancelRequest = false;
		response.body.supportsBreakpointLocationsRequest = false;
		response.body.supportsClipboardContext = false;
		response.body.supportsSteppingGranularity = false;
		response.body.supportsInstructionBreakpoints = false;
		response.body.supportsExceptionFilterOptions = false;
        response.body.supportsSingleThreadExecutionRequests = false;
        this.loginfo("***PROXY->CLIENT - Sending FAKE initialized response");
        // not forwarding message to the server
		this.sendMessageToClient(response);
	}

    private handleLaunchOrAttach(request: DAP.Request) : void {
        if (!this.connected){
            this._socket?.once('connect', () => {
                this.handleLaunchOrAttach(request); 
            });
            return;
        }
        let response = new Response(request);
        this.loginfo("***PROXY->CLIENT - Sending FAKE attach/launch response");
        this.sendMessageToClient(response)
        this.receivedLaunchOrAttachRequest = true;
        // Now we've attached/launched, we fire off the intialized event and we're off to the races
        this.maybeSendFakeInitializedEvent();

    }
	protected handleLaunchRequest(request: DAP.LaunchRequest) : void {
        this.clearExecutionState();
		this.handleLaunchOrAttach(request);
	}
	protected handleAttachRequest(request: DAP.AttachRequest) : void {
        this.clearExecutionState();
		this.handleLaunchOrAttach(request);
	}


	protected handleDisconnectRequest(request: DAP.DisconnectRequest) : void {
		this.sendRequestToServerWithCB(request, 5000, (r: SFDAP.Response) => {
            this.stop();
        });
	}

	protected handleSetBreakpointsRequest(request: DAP.SetBreakpointsRequest) : void {
        let source = request.arguments.source;
        let objectName: string = this.sourceToObjectName(source);

        for (let bpoint of request?.arguments?.breakpoints || []) {
            bpoint.line = this.convertClientLineToDebugger(bpoint.line);
        }
        let sfRequest = request as any;
        sfRequest.arguments.source = objectName;
		this.sendRequestToServerWithCB(request, 10000, (r: SFDAP.Response) => {
            if (r.success == false) {
                // if we timed out, just skip processing
                if (r.message == "timeout") {
                    this.sendMessageToClient(r);
                    return;
                }
                if (!(r.body?.breakpoints?.length > 0)) {
                    let response = r as DAP.SetBreakpointsResponse;
                    // we need to pput the breakpoints back here so the client can mark them as unverified
                    let sourceBpoints = request.arguments.breakpoints || [];
                    response.body = {
                        breakpoints: []
                    }
                    for (let sbp of sourceBpoints) {                
                        let bpoint = {
                            verified: false,
                            line: this.convertDebuggerLineToClient(sbp.line), // this was converted in the request, so we need to convert it back
                            source: request.arguments.source
                        }
                        response.body.breakpoints.push(bpoint);
                    }
                    this.sendMessageToClient(response);
                    return;
                }
            }
            this.handleSetBreakpointsResponse(r as SFDAP.SetBreakpointsResponse);
        });
    }

    // They set body.breakpoints[].source argument to a string instead of a source object, need to fix this
    protected handleSetBreakpointsResponse(message: SFDAP.SetBreakpointsResponse): void{
        if (message.body && message.body.breakpoints){
            message.body.breakpoints.forEach((breakpoint: any)=>{
                breakpoint.line = this.convertDebuggerLineToClient(breakpoint.line);
                let source = this.objectNameToSourceMap.get(breakpoint.source)!;
                breakpoint.source = source;
            });
        }

        this.sendMessageToClient(message);
    }

	protected handleContinueRequest(request: DAP.ContinueRequest) : void {
        this.clearExecutionState();
		this.sendRequestToServerWithCB(request, 10000, (r)=>this._defaultResponseHandler(r))
	}

	protected handleNextRequest(request: DAP.NextRequest) : void {
        this.clearExecutionState();
		this.sendRequestToServerWithCB(request, 10000, (r)=>this._defaultResponseHandler(r))
	}

	protected handleStepInRequest(request: DAP.StepInRequest) : void {
        this.clearExecutionState();
		this.sendRequestToServerWithCB(request, 10000, (r)=>this._defaultResponseHandler(r))
	}

	protected handleStepOutRequest(request: DAP.StepOutRequest) : void {
        this.clearExecutionState();
		this.sendRequestToServerWithCB(request, 10000, (r)=>this._defaultResponseHandler(r))
	}
    
    private _defaultResponseHandler(response: SFDAP.Response) {
        this.sendMessageToClient(response);
    }

	protected handlePauseRequest(request: DAP.PauseRequest) : void {
		this.sendRequestToServerWithCB(request, 5000, (r: SFDAP.Response) => {
            if (r.message === "timeout"){
                // For some reason, it will often not respond to the pause request, so we'll try again
                this.loginfo("***PROXY->SERVER - Resending pause Request!");
                this.sendRequestToServerWithCB(request, 5000, (newr: SFDAP.Response) => {
                    this.handlePauseResponse(newr, request);
                });
            } else {
                this.handlePauseResponse(r, request);
            }
        });
	}

    private handlePauseResponse(r: DAP.PauseResponse, request: DAP.PauseRequest) {
        if (r.success === false && r.message?.startsWith("VM already paused")) {
            // Fake a successful response to get vscode to pause
            r.success = true;
            r.message = "";
            this.loginfo("***PROXY->CLIENT - Pause request did not return, Sending FAKE pause response");
            this.sendMessageToClient(r);
            // then, send a fake stopped event
            let event = <DAP.StoppedEvent>new Event("stopped");
            event.body = {
                reason: "pause",
                threadId: request.arguments.threadId,
                allThreadsStopped: true
            };
            this.loginfo("***PROXY->CLIENT - Sending FAKE stopped event");
            this.sendMessageToClient(event) 
        } else {
            this.sendMessageToClient(r);
        }
    }

    // We shouldn't get these; if we do, we screwed up somewhere.'
    // In either case, starfield doesn't respond to them
    protected handleSourceRequest(request: DAP.SourceRequest) : void{
		this.sendErrorResponse(new Response(request), 1014, 'SOURCE REQUEST?!?!?!?!?', null, ErrorDestination.User);
    }

	protected handleThreadsRequest(request: DAP.ThreadsRequest) : void {
        // Need to handle "threads" request that gets sent while attempting to pause
        // The server returns an error response because starfield refuses to return any threads before the VM is paused
        // So no subsequent pause request is sent
		this.sendRequestToServerWithCB(request, 10000, (r) => {
            this.handleThreadsResponse(r as DAP.ThreadsResponse);
        });
	}

    public handleThreadsResponse(response: DAP.ThreadsResponse) {
        if (!response.success && response.message == "VM is not paused") {
            // Fake a successful response to get vscode to be able to send the pause request
            response.body = {
                threads: this._threads
            }
            response.success = true;
            response.message = "";
            this.loginfo("***PROXY->CLIENT - Threads request failed because VM paused, Sending FAKE threads response.");
        } else if (response.success) {
            if (response.body?.threads?.length > 0){
                // filter out all the no-name threads
                // (threads that have finished execution during pause but haven't been cleaned up yet)
                response.body.threads = response.body.threads.filter((thread)=>{
                    return thread.name != "";
                });
                this._threads = response.body.threads;
            } else { // possible all the threads have ended?
                this._threads = [this.DUMMY_THREAD_OBJ];
            }
        }
        this.sendMessageToClient(response);
    }
    protected getThreadIdFromStackFrameId(stackFrameId: number) : number | undefined {
        return this._stackIdToThreadIdMap.get(stackFrameId);
    }
    protected addStackFrame(threadId: number, frame: DAP.StackFrame) {
        this._stackFrameMap.set(frame.id, frame);
        this._stackIdToThreadIdMap.set(frame.id, threadId);
    }

    protected findFrameForVariableReference(variableReference: number) : DAP.StackFrame | undefined {
        let frameId = this._variableReferencetoFrameIdMap.get(variableReference);
        if (frameId){
            return this._stackFrameMap.get(frameId);
        }
        return undefined;
    }

	protected handleStackTraceRequest(request: DAP.StackTraceRequest) : void {

		this.sendRequestToServerWithCB(request, 10000, (r) => {
            const message = r as SFDAP.StackTraceResponse;
            const threadId = request.arguments.threadId
            let stackframes: DAP.StackFrame[] = []
            if (message.body.stackFrames){
                let index = 0;
                let idBase = threadId * 1000;

                message.body.stackFrames.forEach((frame: any)=>{
                    if (frame.source){
                        if (this.objectNameToSourceMap.has(frame.object)){
                            frame.source = this.objectNameToSourceMap.get(frame.object)!;
                        } else if (fs.existsSync(frame.source)){
                            frame.source = {
                                name: path.basename(frame.source),
                                path: this.convertDebuggerPathToClient(frame.source)
                            } as DAP.Source;
                            this.objectNameToSourceMap.set(frame.object, frame.source);
                        } else {
                            let source = this.FindSourceForObjectName(frame.object);
                            // if we can't find the source, this should be undefined so the client can't try and look it
                            frame.source = source;
                        }
                    } else {
                        let source = this.FindSourceForObjectName(frame.object);
                        frame.source = source;
                    }
                    frame.moduleId = frame.object;
                    if (!frame.line){
                        frame.line = 1;
                    }
                    if (!frame.column){
                        frame.column = 1;
                    }
                    frame.line = this.convertDebuggerLineToClient(frame.line);
                    frame.column = this.convertDebuggerColumnToClient(frame.column);
                    frame.id = idBase + index;
                    this.addStackFrame(threadId, frame as DAP.StackFrame);
                    index++;
                });
            }
            this.sendMessageToClient(message);
        });
	}

    protected GetObjectNameFromScript(abspath : string){
        let objectName = undefined;
        try{
            for (let line of fs.readFileSync(abspath, 'utf8').split(/\r?\n/)){
                if (line.trim().toLowerCase().startsWith("scriptname")) {
                    objectName = line.trim().split(" ")[1];
                    break;
                }
            }
            if (!objectName){
                this.logerror("Did not find script name in file: " + abspath);
            }
        } catch (e) {
            this.logerror("Error reading file "+ abspath, e);
        }
        
        return objectName;
    }
    protected ObjectExistsAtPath(objectName: string, abspath: string) : boolean {
        if (fs.existsSync(abspath)){
            let parsedName = this.GetObjectNameFromScript(abspath);
            if (objectName.toLowerCase() == parsedName?.toLowerCase()){
                return true;
            }
        }
        return false;
    }
    protected FindSourceForObjectName(objectName: string) : DAP.Source | undefined {
        if (this.objectNameToSourceMap.has(objectName))
            return this.objectNameToSourceMap.get(objectName);
        let relpath = objectName.replace(":", path.sep) + ".psc";
        // Time to dig into the workspace folder and see if it's in there
        let abspath = path.join(this.workspaceFolder, relpath);
        if (!this.ObjectExistsAtPath(objectName, abspath)){
            if (!this.BaseScriptFolder){
                return undefined;
            }
            abspath = path.join(this.BaseScriptFolder, relpath);
            if (!this.ObjectExistsAtPath(objectName, abspath)){
                return undefined;
            }
        }
        let source: DAP.Source = {
            name: path.basename(relpath),
            path: this.convertDebuggerPathToClient(abspath)
        }
        this.objectNameToSourceMap.set(objectName, source);
        return source;        
    }

    protected addScopeToScopeMap(scope: ScopeNode) {
        this._scopeMap.set(scope.variablesReference, scope);
        this._variableReferencetoFrameIdMap.set(scope.variablesReference, scope.frameId);
    }

	protected handleScopesRequest(request: DAP.ScopesRequest) : void {
        // TODO: Will need to handle "scope" requests, since starfield doesn't respond to them
        // Translate the scopes into root/path in the custom variable requests and save them as a VariableReference?
        // TODO: Handle
        let scopes: DAP.Scope[] = [];
        let frame = this._stackFrameMap.get(request.arguments.frameId);
        let objectName: string = (frame?.moduleId as string) || ""
        if (frame){
            let localScope = {
                name: "Local",
                presentationHint: "locals",
                variablesReference: this.getVariableRefCount(),
                expensive: false
            } as DAP.Scope;
            scopes.push(localScope);
            let globalScope = {
                name: "Self" + (objectName ? (" (" + objectName + ")") : ("")),
                variablesReference: this.getVariableRefCount(),
                expensive: false
            } as DAP.Scope;
            scopes.push(globalScope);
        }
        let response = <DAP.ScopesResponse> new Response(request);
        response.body = {
            scopes: scopes
        }

        for (let scope of scopes){
            let newScope : ScopeNode = {
                name: scope.name,
                objectName: objectName,
                presentationHint: scope.presentationHint,
                variablesReference: scope.variablesReference,
                threadId: this.getThreadIdFromStackFrameId(request.arguments.frameId) || 0,
                frameId: request.arguments.frameId,
                path: scope.name == "Local" ? [] : ["self"],
                scopeType: scope.name == "Local" ? "local" : "self",
                parentVariableReference: 0,
                expensive: scope.expensive
            }
            this.addScopeToScopeMap(newScope);
        }
        this.loginfo("***PROXY->CLIENT - Sending fake Scopes response to client");
        this.sendMessageToClient(response);
	}

    protected parseOutReflectionInfo(valueStr: string){
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

	protected handleVariablesRequest(request: DAP.VariablesRequest) : void {
        let varReference = request.arguments.variablesReference;
        let scope = this.getScopeFromVariableReference(varReference);
        let frameId = scope?.frameId || -1
        // let frame : any = this.findFrameForVariableReference(varReference);
        let threadId = scope?.threadId || -1
        // let objectName = frame?.object!;
        
        let vrpath: string[] = scope?.path || []
        // TODO: testing
        let stackFrameRoot: SFDAP.Root;
        let realStackIndex = this.getRealStackIndex(frameId, threadId)
        stackFrameRoot = {
            type: "stackFrame",
            threadId: threadId,
            stackFrameIndex: realStackIndex,
        }
        // Testing `value` requests
        // Didn't get much out of this, unfortunately..
        if (scope?.reflectionInfo){
            let TypeInfoRoot: SFDAP.Root = scope.reflectionInfo as SFDAP.Root;

            let newPath = vrpath;
            if (newPath.length > 0 && newPath[0] == "self"){
                let frame = this._stackFrameMap.get(frameId);
                if (frame && frame.moduleId){
                    // newPath = [frame.moduleId.toString()].concat(newPath.slice(1));
                    newPath = [frame.moduleId.toString()];
                }
                // if (scope.propName && newPath.at(-1) == scope.name){
                //     newPath[newPath.length - 1] = scope.propName;
                // }
            }
            newPath = scope.baseForm ? [scope.baseForm] : [];

            let innervarRequest = <SFDAP.VariablesRequest> new Request("variables", {
                root: TypeInfoRoot,
                path: newPath // TODO: This path is incorrect; maybe it's the script name? Scriptname + property name??
            });
            this.sendRequestToServerWithCB(innervarRequest, 10000, (r)=>{
                // TODO: just for testing to see what we get back
                this.loginfo(`^^^^^^^^^^^^^^^^INNER VARIABLE RESPONSE FOR ${scope?.name} - path= ${innervarRequest.arguments.path.join(":")}:`);
            });
            innervarRequest.seq = 0;
            let valueRequest = <SFDAP.VariablesRequest> new Request("value", {
                root: TypeInfoRoot,
                path: newPath // TODO: This path is incorrect; maybe it's the script name? Scriptname + property name??
            });
            valueRequest.seq = 1;
            this.sendRequestToServerWithCB(valueRequest, 10000, (r)=>{
                // TODO: 
                this.loginfo(`^^^^^^^^^^^^^^^^VALUE RESPONSE FOR ${scope?.name} - path= ${valueRequest.arguments.path.join(":")}:`);
            });

        }

        let sfVarsRequest = <SFDAP.VariablesRequest> new Request("variables", {
            root: stackFrameRoot,
            path: vrpath
        });
        sfVarsRequest.seq = request.seq;       

		this.sendRequestToServerWithCB(sfVarsRequest, 10000, (r)=>{
            let response = r as SFDAP.VariablesResponse;
            let newResponse = r as DAP.VariablesResponse;
            let newVariables = [];
            for (let oldVar of response.body?.variables){
                let newVar = {
                    name: oldVar.name,
                    value: oldVar.value,
                    type: oldVar.type,
                    variablesReference: oldVar.compound ? this.getVariableRefCount() : 0 // TODO: verify this
                } as DAP.Variable;
                if (oldVar.name.startsWith("::") && oldVar.name.endsWith("_var")){
                    // strip
                    newVar.name = oldVar.name.substring(2, oldVar.name.length - 4);
                    newVar.presentationHint = {
                        kind: "property"
                    }
                }
                
                if (oldVar.compound){
                    let info = this.parseOutReflectionInfo(newVar.value);
                    // push a new scope
                    let newScope: ScopeNode | undefined = this.getScopeFromVariableReference(newVar.variablesReference);
                    if (!newScope){
                        let _scopeType: "local" | "self" | "parent" | "objectMember" = "objectMember";
                        if (newVar.name.toLowerCase() == "parent"){
                            _scopeType = "parent";
                        } else if (scope?.scopeType == "self"){
                            _scopeType = "objectMember";
                        } else {
                            _scopeType = "local";
                        }
                        newScope = {
                            name: oldVar.name, // Note we need the old variable name here
                            variablesReference: newVar.variablesReference,
                            threadId: threadId,
                            frameId: frameId,
                            path: vrpath.concat([oldVar.name]),
                            scopeType: _scopeType,
                            parentVariableReference: varReference,
                            expensive: false
                        } as ScopeNode
                        if (newVar.presentationHint && newVar.presentationHint.kind == "property") {
                            newScope.propName = newVar.name;
                        }
                    }
                    if (newScope.scopeType == 'self' && scope?.scopeType == 'self'){
                        newScope.objectName = scope?.objectName;
                    }
                    if (info){
                        if (info.root && !newScope.reflectionInfo) {
                            newScope.reflectionInfo = info.root as SFDAP.Root;
                        }
                        if (info.baseForm && !newScope.baseForm) {
                            newScope.baseForm = info.baseForm;
                        }
                    }
                    this._scopeMap.set(newVar.variablesReference, newScope);
                    this._variableMap.set(newVar.variablesReference, newVar);
                }
                newVariables.push(newVar);
            }
            newResponse.body.variables = newVariables;
            this.sendMessageToClient(newResponse);
        })
	}
    private getRealStackIndex(stackId: number, threadId: number) {
        return stackId - (threadId * 1000);
    }

    getStackIdFromVariableReference(varReference: number) {
        return this._variableReferencetoFrameIdMap.get(varReference);
    }
    getScopeFromVariableReference(varReference: number) {
        return this._scopeMap.get(varReference);
    }
    
    // // TODO: this
    // handleVariablesResponse(message: SFDAP.VariablesResponse) {
    //     this.sendMessageToClient(message);
    // }

    // TODO: this
    handleValueResponse(message: SFDAP.ValueResponse) {
        this.sendMessageToClient(message);
    }

	/**
	 * Starfield doesn't actually send back a response when it receives a request it doesn't recognize,
     * so we catch all the requests that starfield doesn't respond to them and return an error
	 */
	protected handleCustomRequest(request: DAP.Request) : void {
		this.sendErrorResponse(new Response(request), 1014, 'unrecognized request', null, ErrorDestination.User);
	}

    convertClientLineToDebugger(line: number) {
        if (this._debuggerLinesStartAt1) {
            return this._clientLinesStartAt1 ? line : line + 1;
        }
        return this._clientLinesStartAt1 ? line - 1 : line;
    }
    convertDebuggerLineToClient(line: number) {
        if (this._debuggerLinesStartAt1) {
            return this._clientLinesStartAt1 ? line : line - 1;
        }
        return this._clientLinesStartAt1 ? line + 1 : line;
    }
    convertClientColumnToDebugger(column: number) {
        if (this._debuggerColumnsStartAt1) {
            return this._clientColumnsStartAt1 ? column : column + 1;
        }
        return this._clientColumnsStartAt1 ? column - 1 : column;
    }
    convertDebuggerColumnToClient(column: number) {
        if (this._debuggerColumnsStartAt1) {
            return this._clientColumnsStartAt1 ? column : column - 1;
        }
        return this._clientColumnsStartAt1 ? column + 1 : column;
    }

    convertClientPathToDebugger(clientPath: string) {
        if (this._clientPathsAreURIs !== this._debuggerPathsAreURIs) {
            if (this._clientPathsAreURIs) {
                return this.uri2path(clientPath);
            }
            else {
                return this.path2uri(clientPath);
            }
        }
        return clientPath;
    }

    convertDebuggerPathToClient(debuggerPath:string) {
        if (this._debuggerPathsAreURIs !== this._clientPathsAreURIs) {
            if (this._debuggerPathsAreURIs) {
                return this.uri2path(debuggerPath);
            }
            else {
                return this.path2uri(debuggerPath);
            }
        }
        return debuggerPath;
    }

    path2uri(path:string) {
        if (process.platform === 'win32') {
            if (/^[A-Z]:/.test(path)) {
                path = path[0].toLowerCase() + path.substr(1);
            }
            path = path.replace(/\\/g, '/');
        }
        path = encodeURI(path);
        let uri = new url.URL(`file:`); // ignore 'path' for now
        uri.pathname = path; // now use 'path' to get the correct percent encoding (see https://url.spec.whatwg.org)
        return uri.toString();
    }
    uri2path(sourceUri: string) {
        let uri = new url.URL(sourceUri);
        let s = decodeURIComponent(uri.pathname);
        if (process.platform === 'win32') {
            if (/^\/[a-zA-Z]:/.test(s)) {
                s = s[1].toLowerCase() + s.substr(2);
            }
            s = s.replace(/\//g, '\\');
        }
        return s;
    }
}