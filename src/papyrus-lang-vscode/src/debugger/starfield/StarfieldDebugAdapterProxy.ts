import { DebugProtocol as DAP } from "@vscode/debugprotocol";
import * as fs from 'fs'
import * as path from 'path'
import { StarfieldDebugProtocol as SFDAP } from "./StarfieldDebugProtocol";
import { DebugAdapterProxy } from "./DebugAdapterProxy";
import { Response, Event, Message } from "@vscode/debugadapter/lib/messages";
import { ScopeNode } from "./StarfieldNodes";


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
export class StarfieldDebugAdapterProxy extends DebugAdapterProxy {
    private readonly DUMMY_THREAD_NAME = "DUMMY THREAD";
    private readonly DUMMY_THREAD_OBJ: DAP.Thread = {
        id: 0,
        name: this.DUMMY_THREAD_NAME
    }

    private _pendingRequests = new Map<number, (response: DAP.Response) => void>();
    private workspaceFolder :string = "";
    private BaseScriptFolder :string | undefined = undefined;
    
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
    constructor(port: number, host:string, startNow: boolean = true, workspaceFolder: string, BaseScriptFolder: string | undefined) {
        super(port, host, startNow);
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
        this.log(`---SERVER->PROXY: ${JSON.stringify(message, undefined, 2)}`);

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
        this.log(`---CLIENT->PROXY: ${JSON.stringify(message, undefined, 2)}`);
        let pmessage = message as DAP.ProtocolMessage
        if (this.outputStream) {
            if (pmessage.type === "request") {
                this.handleClientRequest(pmessage as DAP.Request);
            } else {
                // TODO: handle other message types
                this.sendMessageToServer(pmessage);
            }
        } else {
            // send a terminated event to the client so they disconnect
            this.sendMessageToClient(new Event("terminated"));
        }
    }
    
    handleVersionEvent(message: SFDAP.VersionEvent) {
        // TODO: Do something with this? not very useful
        this.sendMessageToClient(message);
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

    public sendRequestToServerWithCB(request: SFDAP.Request, timeout: number, cb: (response: SFDAP.Response) => void) : void {

		this.sendMessageToServer(request);
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

			} else {
				this.handleCustomRequest(request);
			}
		} catch (e) {
			this.sendErrorResponse(new Response(request), 1104, '{_stack}', e, ErrorDestination.Telemetry);
		}
	}

	protected handleInitializeRequest(request: DAP.InitializeRequest) : void {
        // This is not implemented in Starfield, so we have to intercept and respond ourselves
        // TODO: handle InitializeRequestArguments (like columnStartAt1)

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
        
        // not forwarding message to the server
		this.sendMessageToClient(response);
	}

    private handleLaunchOrAttach(request: DAP.Request) : void {
        this.log("SENDING FAKE attach/launch RESPONSE BACK")
        this.sendMessageToClient(new Response(request))
        // Now we've attached/launched, we fire off the intialized event and we're off to the races
        this.log("SENDING FAKE initialized event BACK")
        this.sendMessageToClient(new Event('initialized'))
        // Don't send message to server

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
                    // we need to actually put the breakpoints back here so the client can mark them as unverified
                    let sourceBpoints = request.arguments.breakpoints || [];
                    response.body = {
                        breakpoints: []
                    }
                    for (let sbp of sourceBpoints) {                
                        let bpoint = {
                            verified: false,
                            line: sbp.line,
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
            this.sendMessageToClient(r);
            // then, send a fake stopped event
            let event = <DAP.StoppedEvent>new Event("stopped");
            event.body = {
                reason: "pause",
                threadId: request.arguments.threadId,
                allThreadsStopped: true
            };
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
                                path: frame.source
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
            path: abspath
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
        let objectName = frame?.moduleId || ""
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
        let scopeNodes : ScopeNode[] = [];
        for (let scope of scopes){
            let newScope : ScopeNode = {
                name: scope.name,
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
        this.log("SENDING FAKE SCOPES TO CLIENT");
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
        if (scope?.reflectionInfo){
            let TypeInfoRoot: SFDAP.Root = scope.reflectionInfo as SFDAP.Root;
            
            let valueRequest = <SFDAP.ValueRequest> new Request("value", {
                root: TypeInfoRoot,
                path: vrpath // TODO: This path is incorrect; maybe it's the script name? Scriptname + property name??
            });
    
            this.sendRequestToServerWithCB(valueRequest, 10000, (r)=>{
                // TODO: just for testing to see what we get back
                this.log("VALUE RESPONSE: ", JSON.stringify(r, undefined, 2));
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
    
    // TODO: this
    handleVariablesResponse(message: SFDAP.VariablesResponse) {
        this.sendMessageToClient(message);
    }

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

}