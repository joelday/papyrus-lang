import { DebugProtocol as DAP } from '@vscode/debugprotocol';
import * as fs from 'fs';
import * as path from 'path';
import { StarfieldDebugProtocol as SFDAP } from './StarfieldDebugProtocol';
import { DebugAdapterProxy, DebugAdapterProxyOptions, colorize_message } from './DebugAdapterProxy';
import { Response, Event, Message } from '@vscode/debugadapter/lib/messages';
import { ScopeNode, StackFrameNode, VariableNode } from './StarfieldNodes';
import * as url from 'url';

export enum ErrorDestination {
    User = 1,
    Telemetry = 2,
}

class Request extends Message implements DAP.Request {
    command: string;
    arguments?: any;
    constructor(command: string, args?: any) {
        super('request');
        this.command = command;
        this.arguments = args;
    }
}

export interface StarfieldDebugAdapterProxyOptions extends DebugAdapterProxyOptions {
    workspaceFolder: string;
    BaseScriptFolder?: string;
}

type responseCallback = (response: SFDAP.Response, request: SFDAP.Request) => void;

interface pendingRequest {
    cb: responseCallback;
    request: DAP.Request;
    noLogResponse: boolean;
}

export class StarfieldDebugAdapterProxy extends DebugAdapterProxy {
    private readonly DUMMY_THREAD_NAME = 'DUMMY THREAD';
    private readonly DUMMY_THREAD_OBJ: DAP.Thread = {
        id: 0,
        name: this.DUMMY_THREAD_NAME,
    };
    // Game name and version from the version event that we currently support
    private readonly STARFIELD_NAME = 'Starfield';
    private readonly STARFIELD_VERSION = 2;
    private readonly SEND_TO_SERVER_CMD = '@toServer:';

    private _pendingRequestsMap = new Map<number, pendingRequest>();
    private workspaceFolder: string = '';
    private BaseScriptFolder: string | undefined = undefined;
    private receivedVersionEvent: boolean = false;
    private receivedLaunchOrAttachRequest: boolean = false;
    private sentInitializedEvent: boolean = false;
    // object name to source map
    protected _objectNameToSourceMap: Map<string, DAP.Source> = new Map<string, DAP.Source>();
    protected _pathtoObjectNameMap: Map<string, string> = new Map<string, string>();
    // TODO: Move state holders to seperate state class
    private _threads: DAP.Thread[] = [this.DUMMY_THREAD_OBJ];
    private _stackFrameMap: Map<number, StackFrameNode> = new Map<number, StackFrameNode>();
    private _stackIdToThreadIdMap: Map<number, number> = new Map<number, number>();
    private _scopeMap: Map<number, ScopeNode> = new Map<number, any>();
    private _variableMap: Map<number, VariableNode> = new Map<number, VariableNode>();
    private _variableReferencetoFrameIdMap: Map<number, number> = new Map<number, number>();
    private _variableRefCount = 0;
    private _localScopeVarRefToSelfScopeVarRefMap: Map<number, number> = new Map<number, number>();

    private currentSeq: number = 0;
    private readonly debuggerPathsAreURIs = false;
    private clientPathsAreURIs: boolean = false;
    private readonly debuggerColumnsStartAt1 = true;
    private clientColumnsStartAt1: boolean = true;
    private readonly debuggerLinesStartAt1 = true;
    private clientLinesStartAt1: boolean = true;
    constructor(options: StarfieldDebugAdapterProxyOptions) {
        const logdir = path.join(
            process.env.USERPROFILE || process.env.HOME || '.',
            'Documents',
            'My Games',
            'Starfield',
            'Logs'
        );
        options.logdir = options.logdir || logdir;
        super(options);
        this.logClientToProxy = 'trace';
        this.logProxyToServer = 'info';
        this.logServerToProxy = 'silent'; // we take care of this ourselves
        this.logProxyToClient = 'info';
    }

    getVariableRefCount() {
        this._variableRefCount++;
        return this._variableRefCount;
    }
    clearExecutionState() {
        this._threads = [this.DUMMY_THREAD_OBJ];
        this._variableRefCount = 0;
        this._stackFrameMap.clear();
        this._scopeMap.clear();
        this._variableMap.clear();
        this._variableReferencetoFrameIdMap.clear();
        this._stackIdToThreadIdMap.clear();
        this._localScopeVarRefToSelfScopeVarRefMap.clear;
    }

    // takes in a Source object and returns the papyrus object idnetifier (e.g. "MyMod:MyScript")
    sourceToObjectName(source: DAP.Source) {
        const name = source.name || '';
        const path = source.path || '';

        let objectName: string = name.split('.')[0];

        // check the object name map path first
        if (this._pathtoObjectNameMap.has(path)) {
            objectName = this._pathtoObjectNameMap.get(path)!;
            this._objectNameToSourceMap.set(objectName, source);
        } else if (path) {
            const newName = this.GetObjectNameFromScript(path);
            if (!newName) {
                this.logerror('Did not find script name in file: ' + path);
            } else {
                objectName = newName;
                this._pathtoObjectNameMap.set(path, objectName);
                this._objectNameToSourceMap.set(objectName, source);
            }
            // set the object name map path
        } else {
            // last ditch; if objectName is in source
            if (this._objectNameToSourceMap.has(objectName)) {
                this._objectNameToSourceMap.set(objectName, source);
            }
        }
        return objectName;
    }

    //overrides base class
    handleMessageFromServer(message: DAP.ProtocolMessage): void {
        if (message.type == 'response') {
            const response = <DAP.Response>message;
            const pending = this._pendingRequestsMap.get(response.request_seq);
            // The callbacks should handle all the responses we need to translate into the expected response objects
            if (pending) {
                if (!pending.noLogResponse) {
                    this.log(this.logServerToProxy, { message }, '---SERVER->PROXY:');
                }
                this._pendingRequestsMap.delete(response.request_seq);
                pending.cb(response, pending.request);
                return;
            }
            // just in case
            this.logwarn('!!!SERVER->PROXY - Received response with no callback!!!');
            this.sendMessageToClient(response);
        } else if (message.type == 'event') {
            const event = message as DAP.Event;
            if (event.event == 'output') {
                this.handleOutputEvent(event as SFDAP.OutputEvent);
            } else if (event.event == 'version') {
                this.handleVersionEvent(event as SFDAP.VersionEvent);
            } else if (event.event == 'thread') {
                this.handleThreadEvent(event as SFDAP.ThreadEvent);
            } else if (event.event == 'stopped') {
                this.handleStoppedEvent(event as SFDAP.StoppedEvent);
            } else {
                this.sendMessageToClient(event);
            }
        } else {
            this.sendMessageToClient(message);
        }
    }

    protected handleStoppedEvent(message: DAP.StoppedEvent) {
        // TODO: do something special here?
        this.sendMessageToClient(message);
    }

    protected handleThreadEvent(message: SFDAP.ThreadEvent): void {
        if (message.body.reason == 'started') {
            // check for existence of dummy thread
            if (this._threads.length == 1 && this._threads[0].name == this.DUMMY_THREAD_NAME) {
                this._threads.pop();
            }
            this._threads.push({
                id: message.body.threadId,
                name: '<thread ' + message.body.threadId + '>',
            });
        } else if (message.body.reason == 'exited') {
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
        const pmessage = message as DAP.ProtocolMessage;
        const retries = 0;
        this.currentSeq = message.seq;
        if (pmessage.type === 'request') {
            this.handleClientRequest(pmessage as DAP.Request);
        } else {
            // TODO: handle other message types
            this.sendMessageToServer(pmessage);
        }
    }

    /**
     * This is the first message sent from the Starfield DAP server when first connecting.
     * Until we get this, we should not send an initialized event from the proxy to the client
     */
    handleVersionEvent(message: SFDAP.VersionEvent) {
        this.receivedVersionEvent = true;
        if (this.STARFIELD_NAME != message.body.game) {
            this.emitOutputEvent('ERROR: Starfield DAP Proxy only supports Starfield, shutting down...', 'important');
            this.stop();
        } else if (this.STARFIELD_VERSION != message.body.version) {
            this.emitOutputEvent(
                `WARNING: Starfield reports unsupported version ${message.body.version}. ` + `Debugging may not work.`,
                'important'
            );
        }
        this.maybeSendFakeInitializedEvent();
    }

    maybeSendFakeInitializedEvent() {
        if (this.receivedVersionEvent && this.receivedLaunchOrAttachRequest && !this.sentInitializedEvent) {
            this.loginfo('***PROXY->CLIENT - SENDING FAKE initialized event');
            this.sendMessageToClient(new Event('initialized'));
            this.sentInitializedEvent = true;
        }
    }

    handleOutputEvent(message: SFDAP.OutputEvent) {
        // The output messages don't have newlines, so just append one.
        // TODO: something with the rest of the fields?
        message.body.output += '\n';
        this.sendMessageToClient(message);
    }

    protected sendErrorResponse(
        response: DAP.Response,
        codeOrMessage: number | DAP.Message,
        format?: string,
        variables?: any,
        dest: ErrorDestination = ErrorDestination.User
    ): void {
        let msg: DAP.Message;
        if (typeof codeOrMessage === 'number') {
            msg = <DAP.Message>{
                id: <number>codeOrMessage,
                format: format,
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
            response.body = {};
        }
        response.body.error = msg;
        this._onError.fire(new Error(response.message));
        this.log(
            'error',
            `***PROXY->CLIENT - Request '${response.command}' (seq: ${response.request_seq}) Failed: ${response.message}`
        );
        this.sendMessageToClient(response, true);
    }

    public sendRequestToServerWithCB(
        request: SFDAP.Request,
        timeout: number,
        cb: responseCallback,
        nolog: boolean = false
    ): void {
        this.sendMessageToServer(request, nolog);
        // check if cb
        this._pendingRequestsMap.set(request.seq, { cb, request, noLogResponse: nolog });
        if (timeout > 0) {
            const timer = setTimeout(() => {
                clearTimeout(timer);
                const pending = this._pendingRequestsMap.get(request.seq);
                if (pending?.cb) {
                    this._pendingRequestsMap.delete(request.seq);
                    pending.cb(new Response(request, 'timeout'), pending.request);
                }
            }, timeout);
        }
    }

    public sendRunInTerminalRequest(
        args: DAP.RunInTerminalRequestArguments,
        timeout: number,
        cb: (response: DAP.RunInTerminalResponse) => void
    ) {
        const request = <DAP.RunInTerminalRequest>new Message('request');
        request.arguments = args;
        this.sendRequestToServerWithCB(request, timeout, (r, req) => {
            r.command = 'runInTerminal';
        });
    }

    protected handleClientRequest(request: DAP.Request): void {
        try {
            if (request.command === 'initialize') {
                this.handleInitializeRequest(<DAP.InitializeRequest>request);
            } else if (request.command === 'launch') {
                this.handleLaunchRequest(<DAP.LaunchRequest>request);
            } else if (request.command === 'attach') {
                this.handleAttachRequest(<DAP.AttachRequest>request);
            } else if (request.command === 'disconnect') {
                this.handleDisconnectRequest(<DAP.DisconnectRequest>request);
            } else if (request.command === 'setBreakpoints') {
                this.handleSetBreakpointsRequest(<DAP.SetBreakpointsRequest>request);
            } else if (request.command === 'continue') {
                this.handleContinueRequest(<DAP.ContinueRequest>request);
            } else if (request.command === 'next') {
                this.handleNextRequest(<DAP.NextRequest>request);
            } else if (request.command === 'stepIn') {
                this.handleStepInRequest(<DAP.StepInRequest>request);
            } else if (request.command === 'stepOut') {
                this.handleStepOutRequest(<DAP.StepOutRequest>request);
            } else if (request.command === 'pause') {
                this.handlePauseRequest(<DAP.PauseRequest>request);
            } else if (request.command === 'stackTrace') {
                this.handleStackTraceRequest(<DAP.StackTraceRequest>request);
            } else if (request.command === 'scopes') {
                this.handleScopesRequest(<DAP.ScopesRequest>request);
            } else if (request.command === 'variables') {
                this.handleVariablesRequest(<DAP.VariablesRequest>request);
            } else if (request.command === 'source') {
                this.handleSourceRequest(<DAP.SourceRequest>request);
            } else if (request.command === 'threads') {
                this.handleThreadsRequest(<DAP.ThreadsRequest>request);
            } else if (request.command === 'value') {
                this.handleValueRequest(<SFDAP.ValueRequest>request);
            } else if (request.command === 'evaluate') {
                this.handleEvaluateRequest(<DAP.EvaluateRequest>request);
            } else {
                this.handleCustomRequest(request);
            }
        } catch (e) {
            this.sendErrorResponse(new Response(request), 1104, '{_stack}', e, ErrorDestination.Telemetry);
        }
    }

    protected handleInitializeRequest(request: DAP.InitializeRequest): void {
        const args = request.arguments;
        if (typeof args.linesStartAt1 === 'boolean') {
            this.clientLinesStartAt1 = args.linesStartAt1;
        }
        if (typeof args.columnsStartAt1 === 'boolean') {
            this.clientColumnsStartAt1 = args.columnsStartAt1;
        }
        if (typeof args.pathFormat === 'string') {
            this.clientPathsAreURIs = args.pathFormat === 'uri';
        }

        const response = <DAP.InitializeResponse>new Response(request);
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
        this.loginfo('***PROXY->CLIENT - Sending FAKE initialized response');
        // not forwarding message to the server
        this.sendMessageToClient(response);
    }

    private handleLaunchOrAttach(request: DAP.Request): void {
        if (!this.connected) {
            this._socket?.once('connect', () => {
                this.handleLaunchOrAttach(request);
            });
            return;
        }
        const response = new Response(request);
        this.loginfo('***PROXY->CLIENT - Sending FAKE attach/launch response');
        this.sendMessageToClient(response);
        this.receivedLaunchOrAttachRequest = true;
        // Now we've attached/launched, we fire off the intialized event and we're off to the races
        this.maybeSendFakeInitializedEvent();
    }
    protected handleLaunchRequest(request: DAP.LaunchRequest): void {
        this.clearExecutionState();
        this.handleLaunchOrAttach(request);
    }
    protected handleAttachRequest(request: DAP.AttachRequest): void {
        this.clearExecutionState();
        this.handleLaunchOrAttach(request);
    }

    protected handleDisconnectRequest(request: DAP.DisconnectRequest): void {
        this.sendRequestToServerWithCB(request, 5000, (r, req) => {
            this.stop();
        });
    }

    protected handleSetBreakpointsRequest(request: DAP.SetBreakpointsRequest): void {
        const source = request.arguments.source;
        const objectName: string = this.sourceToObjectName(source);

        for (const bpoint of request?.arguments?.breakpoints || []) {
            bpoint.line = this.convertClientLineToDebugger(bpoint.line);
        }
        const sfRequest = request as any;
        sfRequest.arguments.source = objectName;
        this.sendRequestToServerWithCB(request, 10000, (r, req) => {
            this.handleSetBreakpointsResponse(r as SFDAP.SetBreakpointsResponse, req as SFDAP.SetBreakpointsRequest);
        });
    }

    // They set body.breakpoints[].source argument to a string instead of a source object, need to fix this
    protected handleSetBreakpointsResponse(
        message: SFDAP.SetBreakpointsResponse,
        request: SFDAP.SetBreakpointsRequest
    ): void {
        if (message.success == false) {
            // if we timed out, just skip processing
            if (message.message == 'timeout') {
                this.sendMessageToClient(message);
                return;
            }
            if (!(message.body?.breakpoints?.length > 0)) {
                const response = message as DAP.SetBreakpointsResponse;
                // we need to pput the breakpoints back here so the client can mark them as unverified
                const sourceBpoints = request.arguments.breakpoints || [];
                response.body = {
                    breakpoints: [],
                };
                for (const sbp of sourceBpoints) {
                    const bpoint = {
                        verified: false,
                        line: this.convertDebuggerLineToClient(sbp.line), // this was converted in the request, so we need to convert it back
                        source: request.arguments.source,
                    } as DAP.Breakpoint;
                    response.body.breakpoints.push(bpoint);
                }
                this.sendMessageToClient(response);
                return;
            }
        }
        if (message.body && message.body.breakpoints) {
            message.body.breakpoints.forEach((breakpoint: any) => {
                breakpoint.line = this.convertDebuggerLineToClient(breakpoint.line);
                const source = this._objectNameToSourceMap.get(breakpoint.source)!;
                breakpoint.source = source;
            });
        }

        this.sendMessageToClient(message);
    }

    protected handleContinueRequest(request: DAP.ContinueRequest): void {
        this.clearExecutionState();
        this.sendRequestToServerWithCB(request, 10000, (r, req) => this._defaultResponseHandler(r));
    }

    protected handleNextRequest(request: DAP.NextRequest): void {
        this.clearExecutionState();
        this.sendRequestToServerWithCB(request, 10000, (r, req) => this._defaultResponseHandler(r));
    }

    protected handleStepInRequest(request: DAP.StepInRequest): void {
        this.clearExecutionState();
        this.sendRequestToServerWithCB(request, 10000, (r, req) => this._defaultResponseHandler(r));
    }

    protected handleStepOutRequest(request: DAP.StepOutRequest): void {
        this.clearExecutionState();
        this.sendRequestToServerWithCB(request, 10000, (r, req) => this._defaultResponseHandler(r));
    }

    private _defaultResponseHandler(response: SFDAP.Response) {
        this.sendMessageToClient(response);
    }

    protected handlePauseRequest(request: DAP.PauseRequest): void {
        this.sendRequestToServerWithCB(request, 5000, (r: SFDAP.Response, req) => {
            if (r.message === 'timeout') {
                // For some reason, it will often not respond to the pause request, so we'll try again
                this.loginfo('***PROXY->SERVER - Resending pause Request!');
                this.sendRequestToServerWithCB(request, 5000, (newr: SFDAP.Response, _newreq) => {
                    this.handlePauseResponse(newr, request); // use the original request so that the client gets the right request_seq in the response
                });
            } else {
                this.handlePauseResponse(r, request); // ditto
            }
        });
    }

    private handlePauseResponse(response: DAP.PauseResponse, request: DAP.PauseRequest) {
        if (response.success === false && response.message?.startsWith('VM already paused')) {
            // Fake a successful response to get vscode to pause
            response.success = true;
            response.message = '';
            this.loginfo('***PROXY->CLIENT - Pause request did not return successfully, Sending FAKE pause response');
            this.sendMessageToClient(response);
            // then, send a fake stopped event
            const event = <DAP.StoppedEvent>new Event('stopped');
            event.body = {
                reason: 'pause',
                threadId: request.arguments.threadId,
                allThreadsStopped: true,
            };
            this.loginfo('***PROXY->CLIENT - Sending FAKE stopped event');
            this.sendMessageToClient(event);
        } else {
            this.sendMessageToClient(response);
        }
    }

    // We shouldn't get these; if we do, we screwed up somewhere.'
    // In either case, starfield doesn't respond to them
    protected handleSourceRequest(request: DAP.SourceRequest): void {
        this.sendErrorResponse(new Response(request), 1015, 'SOURCE REQUEST?!?!?!?!?', null, ErrorDestination.User);
    }

    protected handleThreadsRequest(request: DAP.ThreadsRequest): void {
        // Need to handle "threads" request that gets sent while attempting to pause
        // The server returns an error response because starfield refuses to return any threads before the VM is paused
        // So no subsequent pause request is sent
        this.sendRequestToServerWithCB(request, 10000, (r, req) => {
            this.handleThreadsResponse(r as DAP.ThreadsResponse);
        });
    }

    public handleThreadsResponse(response: DAP.ThreadsResponse) {
        if (!response.success && response.message == 'VM is not paused') {
            // Fake a successful response to get vscode to be able to send the pause request
            response.body = {
                threads: this._threads,
            };
            response.success = true;
            response.message = '';
            this.loginfo('***PROXY->CLIENT - Threads request failed because VM paused, Sending FAKE threads response.');
        } else if (response.success) {
            if (response.body?.threads?.length > 0) {
                // filter out all the no-name threads
                // (threads that have finished execution during pause but haven't been cleaned up yet)
                response.body.threads = response.body.threads.filter((thread) => {
                    return thread.name != '';
                });
                this._threads = response.body.threads;
            } else {
                // possible all the threads have ended?
                this._threads = [this.DUMMY_THREAD_OBJ];
            }
        }
        this.sendMessageToClient(response);
    }

    protected handleStackTraceRequest(request: DAP.StackTraceRequest): void {
        this.sendRequestToServerWithCB(request, 10000, (r, req) =>
            this.handleStackTraceResponse(r as SFDAP.StackTraceResponse, req as SFDAP.StackTraceRequest)
        );
    }

    protected handleStackTraceResponse(response: SFDAP.StackTraceResponse, request: SFDAP.StackTraceRequest) {
        const threadId = request.arguments.threadId;
        if (response.body.stackFrames) {
            let index = 0;
            const dapStackFrames: DAP.StackFrame[] = [];
            response.body.stackFrames.forEach((frame: any) => {
                const stackId = this.addStackFrame(threadId, index, frame);
                dapStackFrames.push(this._stackFrameMap.get(stackId)!.getStandardDAPFrame());
                index++;
            });
            (response.body as any).stackFrames = dapStackFrames;
        }
        this.sendMessageToClient(response);
    }

    /**
     * Not responded to by the server, so we need to fake it
     * @param request
     */
    protected handleScopesRequest(request: DAP.ScopesRequest): void {
        const scopeNodes = this.makeScopesforStackFrame(request.arguments.frameId);
        if (!scopeNodes || scopeNodes.length == 0) {
            this.sendErrorResponse(
                new Response(request),
                1403,
                'Could not find frame for frameId: {frameId}',
                { frameId: request.arguments.frameId },
                ErrorDestination.User
            );
            return;
        }
        const response = <DAP.ScopesResponse>new Response(request);
        response.body = {
            scopes: scopeNodes.map((scope) => {
                return scope.getDAPScope();
            }),
        };
        this.loginfo('***PROXY->CLIENT - Sending fake Scopes response to client');
        this.sendMessageToClient(response);
    }

    protected handleVariablesRequest(request: DAP.VariablesRequest): void {
        const varReference = request.arguments.variablesReference;
        const scope = this.getScopeFromVariableReference(varReference);
        if (!scope) {
            this.sendErrorResponse(
                new Response(request),
                2001,
                `Non-existent scope for variable reference: ${varReference}`,
                undefined,
                ErrorDestination.Telemetry
            );
            return;
        }
        if (scope.scopeType != 'reflectionItem') {
            const stackFrame = this._stackFrameMap.get(scope.frameId);
            if (!stackFrame || stackFrame === undefined) {
                this.sendErrorResponse(
                    new Response(request),
                    2004,
                    `SOMEHOW?!?! Non-existent stack frame for variable reference: ${varReference}`,
                    undefined,
                    ErrorDestination.Telemetry
                );
                return;
            }
            // TODO: TESTING, REMOVE
            if (scope?.scopeType != 'localEnclosure') {
                this.testValuesReqs(scope, scope.path, scope.frameId);
            }
            const stackFrameRoot = {
                type: 'stackFrame',
                threadId: stackFrame.threadId,
                stackFrameIndex: stackFrame.realStackIndex,
            };
            const sfVarsRequest = <SFDAP.VariablesRequest>new Request('variables', {
                root: stackFrameRoot,
                path: scope.path,
            });
            sfVarsRequest.seq = request.seq;

            this.sendRequestToServerWithCB(sfVarsRequest, 10000, (r, req) =>
                this.handleVariablesResponse(r as SFDAP.VariablesResponse, req as SFDAP.VariablesRequest, varReference)
            );
            return;
        }
        // This is a reflection item...
        // TODO: handle this
        this.sendErrorResponse(
            new Response(request),
            2002,
            `Reflection item variables not yet supported!`,
            undefined,
            ErrorDestination.Telemetry
        );
    }

    private testValuesReqs(scope: ScopeNode, vrpath: string[], frameId: number) {
        const variable = this._variableMap.get(scope.variablesReference);
        if (!variable) {
            return;
        }
        const TypeInfoRoot: SFDAP.Root = variable.reflectionInfo as SFDAP.Root;

        let newPath = vrpath;
        if (newPath.length > 0 && newPath[0] == 'self') {
            const frame = this._stackFrameMap.get(frameId);
            if (frame && frame.moduleId) {
                // newPath = [frame.moduleId.toString()].concat(newPath.slice(1));
                newPath = [frame.moduleId.toString()];
            }
        }
        newPath = variable.baseForm ? [variable.baseForm] : [];

        const innervarRequest = <SFDAP.VariablesRequest>new Request('variables', {
            root: TypeInfoRoot,
            path: newPath, // TODO: This path is incorrect; maybe it's the script name? Scriptname + property name??
        });
        this.sendRequestToServerWithCB(
            innervarRequest,
            10000,
            (r) => {
                // TODO: just for testing to see what we get back
                this.loginfo(
                    { message: r },
                    `vvvvvvvvvvvvvvvvv INNER VARIABLE RESPONSE FOR ${scope?.name} - path= ${innervarRequest.arguments.path.join(
                        ':'
                    )}:`
                );
            },
            false
        );
        innervarRequest.seq = 0;
        const valueRequest = <SFDAP.VariablesRequest>new Request('value', {
            root: TypeInfoRoot,
            path: newPath, // TODO: This path is incorrect; maybe it's the script name? Scriptname + property name??
        });
        valueRequest.seq = 1;
        this.sendRequestToServerWithCB(
            valueRequest,
            10000,
            (r) => {
                // TODO: just for testing to see what we get back
                this.loginfo(
                    { message: r },
                    `vvvvvvvvvvvvvvvvv VALUE RESPONSE FOR ${scope?.name} - path= ${valueRequest.arguments.path.join(
                        ':'
                    )}:`
                );
            },
            false
        );
    }

    handleVariablesResponse(response: SFDAP.VariablesResponse, request: SFDAP.VariablesRequest, varReference: number) {
        const parentScope = this.getScopeFromVariableReference(varReference);
        if (!parentScope) {
            this.sendErrorResponse(
                new Response(request),
                2003,
                `SOMEHOW?!?! Non-existent scope for variable reference: ${varReference}`,
                undefined,
                ErrorDestination.Telemetry
            );
            return;
        }
        const stackFrame = this._stackFrameMap.get(parentScope.frameId);
        if (!stackFrame || stackFrame === undefined) {
            this.sendErrorResponse(
                new Response(request),
                2004,
                `SOMEHOW?!?! Non-existent stack frame for variable reference: ${varReference}`,
                undefined,
                ErrorDestination.Telemetry
            );
            return;
        }

        const newResponse = response as any;
        newResponse.body.variables = this.addVariablesToState(response.body.variables, stackFrame, parentScope).map(
            (varNode) => {
                return varNode.getDAPVariable();
            }
        );
        this.sendMessageToClient(newResponse);
    }

    handleValueRequest(request: SFDAP.ValueRequest) {
        // This came from the REPL console, just send it to the server
        this.sendRequestToServerWithCB(request, 10000, (r, req) => {
            if (r.success != false) {
                // failed responses will show in the REPL console by themselves
                this.emitOutputEvent(
                    `Response to REPL variables request (path: ${req.arguments.path.join('.')}):\n${colorize_message(
                        r.body
                    )}`,
                    'console'
                );
            } else {
                this.log('warn', { r }, '***PROXY->CLIENT FAILED RESPONSE:');
            }
            this.sendMessageToClient(r, true);
        });
    }

    // TODO: this
    handleValueResponse(message: SFDAP.ValueResponse, request: SFDAP.ValueRequest) {
        this.sendMessageToClient(message);
    }

    handleEvaluateRequest(request: DAP.EvaluateRequest) {
        try {
            const expr = request.arguments.expression.trim();
            if (request.arguments.context == 'repl') {
                if (expr.startsWith(this.SEND_TO_SERVER_CMD)) {
                    this.handleREPLSendToServer(request);
                    return;
                }
            }
            // Straight-up expression, make a value request
            // TODO: Not implemented yet
            this.sendErrorResponse(
                new Response(request),
                1109,
                'Evaluation of values is not implemented',
                null,
                ErrorDestination.Telemetry
            );
        } catch (e) {
            this.sendErrorResponse(
                new Response(request),
                1109,
                'Invalid expression in REPL message!',
                null,
                ErrorDestination.Telemetry
            );
        }
    }

    // Using this for debugging/RE; in the debug console of the client, you can type in server requests and they will be sent to the server
    private handleREPLSendToServer(evalRequest: DAP.EvaluateRequest) {
        try {
            const expr = evalRequest.arguments.expression.trim();
            const messageToSend: DAP.ProtocolMessage = JSON.parse(expr.replace(this.SEND_TO_SERVER_CMD, '').trim());
            //check that it's a valid DAP.ProtocolMessage
            // make sure we have a valid sequence number
            messageToSend.seq = 10000 + this.currentSeq;
            if (messageToSend.type) {
                // send it to the server
                this.loginfo('!!!PROXY->SERVER - Sending message from REPL console to server!!!');
                if (messageToSend.type == 'request') {
                    const sreq = messageToSend as DAP.Request;
                    if (!sreq.command) {
                        this.sendErrorResponse(
                            new Response(evalRequest),
                            1105,
                            'Invalid server request!',
                            null,
                            ErrorDestination.User
                        );
                    }
                    // special handler for variableRequest
                    if (
                        sreq.command == 'variables' &&
                        sreq?.arguments?.hasOwnProperty('root') &&
                        sreq?.arguments?.hasOwnProperty('path')
                    ) {
                        // formatted correctly, send it to the server
                        this.sendRequestToServerWithCB(sreq as SFDAP.VariablesRequest, 10000, (r, req) => {
                            // this was sent from a REPL
                            if (r.success != false) {
                                // failed responses will show in the REPL console by themselves
                                this.emitOutputEvent(
                                    `Response to REPL variables request (path: ${req.arguments.path.join(
                                        '.'
                                    )}):\n${colorize_message(r.body.variables)}`,
                                    'console'
                                );
                            }
                            // TODO: do something else other than sending the response straight back
                            this.sendMessageToClient(r);
                            return;
                        });
                    } else {
                        this.handleClientRequest(sreq);
                    }
                } else {
                    this.sendMessageToServer(messageToSend as DAP.ProtocolMessage);
                }
            } else {
                this.sendErrorResponse(
                    new Response(evalRequest),
                    1106,
                    'Invalid server message!',
                    null,
                    ErrorDestination.User
                );
            }
        } catch (e) {
            this.sendErrorResponse(
                new Response(evalRequest),
                1107,
                'Invalid JSON in REPL Send to Server command!',
                e,
                ErrorDestination.Telemetry
            );
        }
    }

    /**
     * Starfield doesn't actually send back a response when it receives a request it doesn't recognize,
     * so we catch all the requests that starfield doesn't respond to them and return an error
     */
    protected handleCustomRequest(request: DAP.Request): void {
        this.sendErrorResponse(new Response(request), 1014, 'unrecognized request', null, ErrorDestination.User);
    }

    // formatting functions
    private static _formatPIIRegexp = /{([^}]+)}/g;
    private static formatPII(format: string, excludePII: boolean, args: { [key: string]: string }): string {
        return format.replace(StarfieldDebugAdapterProxy._formatPIIRegexp, function (match, paramName) {
            if (excludePII && paramName.length > 0 && paramName[0] !== '_') {
                return match;
            }
            return args[paramName] && args.hasOwnProperty(paramName) ? args[paramName] : match;
        });
    }

    convertClientLineToDebugger(line: number) {
        if (this.debuggerLinesStartAt1) {
            return this.clientLinesStartAt1 ? line : line + 1;
        }
        return this.clientLinesStartAt1 ? line - 1 : line;
    }
    convertDebuggerLineToClient(line: number) {
        if (this.debuggerLinesStartAt1) {
            return this.clientLinesStartAt1 ? line : line - 1;
        }
        return this.clientLinesStartAt1 ? line + 1 : line;
    }
    convertClientColumnToDebugger(column: number) {
        if (this.debuggerColumnsStartAt1) {
            return this.clientColumnsStartAt1 ? column : column + 1;
        }
        return this.clientColumnsStartAt1 ? column - 1 : column;
    }
    convertDebuggerColumnToClient(column: number) {
        if (this.debuggerColumnsStartAt1) {
            return this.clientColumnsStartAt1 ? column : column - 1;
        }
        return this.clientColumnsStartAt1 ? column + 1 : column;
    }

    convertClientPathToDebugger(clientPath: string) {
        if (this.clientPathsAreURIs !== this.debuggerPathsAreURIs) {
            if (this.clientPathsAreURIs) {
                return this.uri2path(clientPath);
            } else {
                return this.path2uri(clientPath);
            }
        }
        return clientPath;
    }

    convertDebuggerPathToClient(debuggerPath: string) {
        if (this.debuggerPathsAreURIs !== this.clientPathsAreURIs) {
            if (this.debuggerPathsAreURIs) {
                return this.uri2path(debuggerPath);
            } else {
                return this.path2uri(debuggerPath);
            }
        }
        return debuggerPath;
    }

    path2uri(path: string) {
        if (process.platform === 'win32') {
            if (/^[A-Z]:/.test(path)) {
                path = path[0].toLowerCase() + path.substr(1);
            }
            path = path.replace(/\\/g, '/');
        }
        path = encodeURI(path);
        const uri = new url.URL(`file:`); // ignore 'path' for now
        uri.pathname = path; // now use 'path' to get the correct percent encoding (see https://url.spec.whatwg.org)
        return uri.toString();
    }
    uri2path(sourceUri: string) {
        const uri = new url.URL(sourceUri);
        let s = decodeURIComponent(uri.pathname);
        if (process.platform === 'win32') {
            if (/^\/[a-zA-Z]:/.test(s)) {
                s = s[1].toLowerCase() + s.substr(2);
            }
            s = s.replace(/\//g, '\\');
        }
        return s;
    }

    // TODO: move these of these to a seperate State holder
    // STATE FUNCTIONS
    private getSource(object: string, filePath?: string) {
        if (filePath) {
            if (this._objectNameToSourceMap.has(object)) {
                return this._objectNameToSourceMap.get(object)!;
            } else if (fs.existsSync(filePath)) {
                const source = {
                    name: path.basename(filePath),
                    path: this.convertDebuggerPathToClient(filePath),
                } as DAP.Source;
                this._objectNameToSourceMap.set(object, source);
                return source;
            } else {
                // if we can't find the source, this should be undefined so the client can't try and look it
                return this.FindSourceForObjectName(object);
            }
        } else {
            return this.FindSourceForObjectName(object);
        }
    }

    protected GetObjectNameFromScript(abspath: string) {
        let objectName = undefined;
        try {
            for (const line of fs.readFileSync(abspath, 'utf8').split(/\r?\n/)) {
                if (line.trim().toLowerCase().startsWith('scriptname')) {
                    objectName = line.trim().split(' ')[1];
                    break;
                }
            }
            if (!objectName) {
                this.logerror('Did not find script name in file: ' + abspath);
            }
        } catch (e) {
            this.logerror('Error reading file ' + abspath, e);
        }

        return objectName;
    }
    protected ObjectExistsAtPath(objectName: string, abspath: string): boolean {
        if (fs.existsSync(abspath)) {
            const parsedName = this.GetObjectNameFromScript(abspath);
            if (objectName.toLowerCase() == parsedName?.toLowerCase()) {
                return true;
            }
        }
        return false;
    }
    protected FindSourceForObjectName(objectName: string): DAP.Source | undefined {
        if (this._objectNameToSourceMap.has(objectName)) return this._objectNameToSourceMap.get(objectName);
        const relpath = objectName.replace(':', path.sep) + '.psc';
        // Time to dig into the workspace folder and see if it's in there
        let abspath = path.join(this.workspaceFolder, relpath);
        if (!this.ObjectExistsAtPath(objectName, abspath)) {
            if (!this.BaseScriptFolder) {
                return undefined;
            }
            abspath = path.join(this.BaseScriptFolder, relpath);
            if (!this.ObjectExistsAtPath(objectName, abspath)) {
                return undefined;
            }
        }
        const source: DAP.Source = {
            name: path.basename(relpath),
            path: this.convertDebuggerPathToClient(abspath),
        };
        this._objectNameToSourceMap.set(objectName, source);
        return source;
    }
    protected getThreadIdFromStackFrameId(stackFrameId: number): number | undefined {
        return this._stackIdToThreadIdMap.get(stackFrameId);
    }
    protected addStackFrame(threadId: number, stackIndex: number, frame: SFDAP.StackFrame): number {
        const Source = this.getSource(frame.object, frame.source);
        const StackNode = new StackFrameNode(frame, threadId, stackIndex, Source);
        this._stackFrameMap.set(StackNode.id, StackNode);
        this._stackIdToThreadIdMap.set(StackNode.id, threadId);
        return StackNode.id;
    }

    protected findFrameForVariableReference(variableReference: number): DAP.StackFrame | undefined {
        const frameId = this._variableReferencetoFrameIdMap.get(variableReference);
        if (frameId) {
            return this._stackFrameMap.get(frameId);
        }
        return undefined;
    }
    protected addScopeToScopeMap(scope: ScopeNode) {
        this._scopeMap.set(scope.variablesReference, scope);
        this._variableReferencetoFrameIdMap.set(scope.variablesReference, scope.frameId);
    }

    protected findScopesForFrame(frameId: number): ScopeNode[] {
        return Array.from(this._scopeMap.values()).filter((scope) => {
            return scope.frameId == frameId;
        });
    }
    protected makeScopesforStackFrame(frameId: number): ScopeNode[] | undefined {
        const frame = this._stackFrameMap.get(frameId);
        if (!frame) {
            return undefined;
        }
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
    private getRealStackIndex(stackId: number, threadId: number) {
        return stackId - threadId * 1000;
    }

    getStackIdFromVariableReference(varReference: number) {
        return this._variableReferencetoFrameIdMap.get(varReference);
    }
    getScopeFromVariableReference(varReference: number) {
        return this._scopeMap.get(varReference);
    }
    getSelfScopeRef(localScopeVarRef: number) {
        return this._localScopeVarRefToSelfScopeVarRefMap.get(localScopeVarRef);
    }

    addVariablesToState(
        variables: SFDAP.Variable[],
        stackFrame: StackFrameNode,
        parentScope: ScopeNode
    ): VariableNode[] {
        const newVariables = [];
        for (const oldVar of variables) {
            const varNode = new VariableNode(oldVar, this.getVariableRefCount(), false, parentScope);
            if (oldVar.compound) {
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
                            this.logwarn('Could not find self scope for variable reference: ' + selfVarRef);
                        }
                    }
                }
                const ScopeFromVariable: ScopeNode = ScopeNode.ScopeFromVariable(varNode, stackFrame, parentScope);
                this._scopeMap.set(varNode.variablesReference, ScopeFromVariable);
                this._variableMap.set(varNode.variablesReference, varNode);
            }
            newVariables.push(varNode);
        }
        return newVariables;
    }
    // END STATE FUNCTIONS
}
