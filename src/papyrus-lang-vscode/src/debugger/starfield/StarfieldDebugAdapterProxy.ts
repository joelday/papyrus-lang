/* eslint-disable @typescript-eslint/no-explicit-any */
/* eslint-disable no-prototype-builtins */
import { DebugProtocol as DAP } from '@vscode/debugprotocol';
import * as fs from 'fs';
import * as path from 'path';
import { StarfieldDebugProtocol as SFDAP } from './StarfieldDebugProtocol';
import { DebugAdapterProxy, DebugAdapterProxyOptions, colorize_message } from './DebugAdapterProxy';
import { Response, Event, Message } from '@vscode/debugadapter/lib/messages';
import { StateNode } from './StarfieldNodes';

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

const STARFIELD_DAP_LOCALE = {
    linesStartAt1: true,
    columnsStartAt1: true,
    pathsAreURIs: false,
};

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
    private readonly DEBUGGER_CAPABILITIES: DAP.Capabilities = {
        supportsConfigurationDoneRequest: false,
        supportsFunctionBreakpoints: false,
        supportsConditionalBreakpoints: false,
        supportsHitConditionalBreakpoints: false,
        supportsEvaluateForHovers: false,
        exceptionBreakpointFilters: [],
        supportsStepBack: false,
        supportsSetVariable: false,
        supportsRestartFrame: false,
        supportsGotoTargetsRequest: false,
        supportsStepInTargetsRequest: false,
        supportsCompletionsRequest: false,
        completionTriggerCharacters: [],
        supportsModulesRequest: false,
        additionalModuleColumns: [],
        supportedChecksumAlgorithms: [],
        supportsRestartRequest: false,
        supportsExceptionOptions: false,
        supportsValueFormattingOptions: false,
        supportsExceptionInfoRequest: false,
        supportTerminateDebuggee: false,
        supportSuspendDebuggee: false,
        supportsDelayedStackTraceLoading: false,
        supportsLoadedSourcesRequest: false,
        supportsLogPoints: false,
        supportsTerminateThreadsRequest: false,
        supportsSetExpression: false,
        supportsTerminateRequest: false,
        supportsDataBreakpoints: false,
        supportsReadMemoryRequest: false,
        supportsWriteMemoryRequest: false,
        supportsDisassembleRequest: false,
        supportsCancelRequest: false,
        supportsBreakpointLocationsRequest: false,
        supportsClipboardContext: false,
        supportsSteppingGranularity: false,
        supportsInstructionBreakpoints: false,
        supportsExceptionFilterOptions: false,
        supportsSingleThreadExecutionRequests: false,
    };

    private _pendingRequestsMap = new Map<number, pendingRequest>();
    private workspaceFolder: string = '';
    private BaseScriptFolder: string | undefined = undefined;
    private receivedVersionEvent: boolean = false;
    private receivedLaunchOrAttachRequest: boolean = false;
    private sentInitializedEvent: boolean = false;
    private firstTimeReceivingScopes: boolean = true;
    // object name to source map
    protected _objectNameToSourceMap: Map<string, DAP.Source> = new Map<string, DAP.Source>();
    protected _pathtoObjectNameMap: Map<string, string> = new Map<string, string>();

    protected stateNode: StateNode;

    private currentSeq: number = 0;
    constructor(options: StarfieldDebugAdapterProxyOptions) {
        const logdir = path.join(
            process.env.USERPROFILE || process.env.HOME || '.',
            'Documents',
            'My Games',
            'Starfield',
            'Logs',
            'DAProxy'
        );
        options.logdir = options.logdir || logdir;
        options.debuggerLocale = STARFIELD_DAP_LOCALE;
        super(options);
        this.clientCaps.adapterID = 'papyrus';
        this.logClientToProxy = 'trace';
        this.logProxyToServer = 'info';
        this.logServerToProxy = 'silent'; // we take care of this ourselves
        this.logProxyToClient = 'info';
        this.stateNode = new StateNode();
    }
    clearExecutionState() {
        this.stateNode.clear();
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
            this.stateNode.addThread(message.body.threadId);
        } else if (message.body.reason == 'exited') {
            this.stateNode.removeThread(message.body.threadId);
        }
        this.sendMessageToClient(message);
    }

    //overrides base class
    protected handleMessageFromClient(message: DAP.ProtocolMessage): void {
        const pmessage = message as DAP.ProtocolMessage;
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
        response.message = DebugAdapterProxy.formatPII(msg.format, true, msg.variables || {});
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
        _cb: (response: DAP.RunInTerminalResponse) => void
    ) {
        const request = <DAP.RunInTerminalRequest>new Message('request');
        request.arguments = args;
        this.sendRequestToServerWithCB(request, timeout, (r, _req) => {
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
        this.setClientCapabilities(args);
        const response = <DAP.InitializeResponse>new Response(request);
        response.body = response.body || {};

        response.body = this.DEBUGGER_CAPABILITIES;
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
        this.sendRequestToServerWithCB(request, 5000, (_r, _req) => {
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
        this.sendRequestToServerWithCB(request, 10000, (r, _req) => this._defaultResponseHandler(r));
    }

    protected handleNextRequest(request: DAP.NextRequest): void {
        this.clearExecutionState();
        this.sendRequestToServerWithCB(request, 10000, (r, _req) => this._defaultResponseHandler(r));
    }

    protected handleStepInRequest(request: DAP.StepInRequest): void {
        this.clearExecutionState();
        this.sendRequestToServerWithCB(request, 10000, (r, _req) => this._defaultResponseHandler(r));
    }

    protected handleStepOutRequest(request: DAP.StepOutRequest): void {
        this.clearExecutionState();
        this.sendRequestToServerWithCB(request, 10000, (r, _req) => this._defaultResponseHandler(r));
    }

    private _defaultResponseHandler(response: SFDAP.Response) {
        this.sendMessageToClient(response);
    }

    protected handlePauseRequest(request: DAP.PauseRequest): void {
        this.sendRequestToServerWithCB(request, 5000, (r: SFDAP.Response, _req) => {
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
        this.sendRequestToServerWithCB(request, 10000, (r, _req) => {
            this.handleThreadsResponse(r as DAP.ThreadsResponse);
        });
    }

    public handleThreadsResponse(response: DAP.ThreadsResponse) {
        if (!response.success && response.message == 'VM is not paused') {
            // Fake a successful response to get vscode to be able to send the pause request
            response.body = {
                threads: this.stateNode.getThreads(),
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
                this.stateNode.setThreads(response.body.threads);
            } else {
                this.stateNode.setThreads([]);
                response.body.threads = this.stateNode.getThreads();
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

            response.body.stackFrames.forEach((frame: SFDAP.StackFrame) => {
                const SFDAPsource = frame.source;
                const source = this.getSource(frame.object, SFDAPsource);
                if (source) {
                    source.path = this.convertDebuggerPathToClient(source?.path || '');
                }
                const dapStack = this.stateNode.addStackFrame(frame, threadId, index, source);
                dapStack.column = this.convertDebuggerColumnToClient(dapStack.column);
                dapStack.line = this.convertDebuggerLineToClient(dapStack.line);
                dapStackFrames.push(dapStack);
                index++;
            });
            (response.body as any).stackFrames = dapStackFrames;
        }
        this.sendMessageToClient(response);
    }

    /**
     * Not responded to by the server, so we need to fake it
     * @param scopesRequest
     */
    protected handleScopesRequest(scopesRequest: DAP.ScopesRequest): void {
        const localScope = this.stateNode.makeLocalScopeForStackFrame(scopesRequest.arguments.frameId);
        if (!localScope) {
            this.sendErrorResponse(
                new Response(scopesRequest),
                1403,
                'Could not find frame for frameId: {frameId}',
                { frameId: scopesRequest.arguments.frameId },
                ErrorDestination.User
            );
            return;
        }
        // now we make a variables request for the local scope

        const args = this.stateNode.getVariablesArgumentsForScope(localScope.variablesReference);
        if (!args) {
            this.sendErrorResponse(
                new Response(scopesRequest),
                1403,
                'Could not find variables arguments for scope: {scope}',
                { scope: localScope },
                ErrorDestination.User
            );
            return;
        }
        const variablesRequest = <SFDAP.VariablesRequest>new Request('variables', args);
        this.sendRequestToServerWithCB(variablesRequest, 10000, (r, _req) => {
            if (!r.success) {
                this.sendErrorResponse(
                    new Response(scopesRequest),
                    1403,
                    'Could not get variables for scope: {scope}',
                    { scope: localScope },
                    ErrorDestination.User
                );
                return;
            }
            // we only care about getting the `self` scope out of this
            const scopes = [localScope];
            const variablesResponse = r as SFDAP.VariablesResponse;
            const respVariables = variablesResponse.body.variables;
            const dapVars = this.stateNode.addVariablesToState(
                respVariables,
                localScope.variablesReference,
                scopesRequest.arguments.frameId
            );
            const selfVariable = dapVars.find((variable) => {
                return variable.name.toLowerCase() == 'self';
            });
            // if we didn't find a self variable, this must be a static script; don't worry about it
            if (selfVariable && selfVariable.variablesReference) {
                const selfScope = this.stateNode.getScope(selfVariable.variablesReference);
                scopes.push(selfScope);
            }
            const response = <DAP.ScopesResponse>new Response(scopesRequest);
            response.body = {
                scopes: scopes,
            };
            this.sendMessageToClient(response);
            if (this.firstTimeReceivingScopes) {
                this.emitOutputEvent(
                    'WARNING: Due to limitations in the DAP server built into Starfield, ' +
                        'form reflection is limited. Most form objects that do not have ' +
                        'scripts attached to them will only show the current object state ("::state").\n',
                    'console'
                );
                this.firstTimeReceivingScopes = false;
            }
        });
    }

    protected handleVariablesRequest(request: DAP.VariablesRequest): void {
        const varReference = request.arguments.variablesReference;
        if (!this.stateNode.hasScope(varReference)) {
            this.sendErrorResponse(
                new Response(request),
                2001,
                `Non-existent scope for variable reference: ${varReference}`,
                undefined,
                ErrorDestination.Telemetry
            );
            return;
        }
        // TODO: Handle reflection items
        const args = this.stateNode.getVariablesArgumentsForScope(varReference);
        const sfVarsRequest = <SFDAP.VariablesRequest>new Request('variables', args);
        sfVarsRequest.seq = request.seq;

        this.sendRequestToServerWithCB(sfVarsRequest, 10000, (r, req) => {
            this.handleVariablesResponse(r as SFDAP.VariablesResponse, req as SFDAP.VariablesRequest, varReference);
        });
        return;
    }

    handleVariablesResponse(response: SFDAP.VariablesResponse, request: SFDAP.VariablesRequest, varReference: number) {
        if (!response.success) {
            this.sendMessageToClient(response);
            return;
        }
        const newResponse = response as any;

        newResponse.body.variables = this.stateNode.addVariablesToState(
            response.body.variables,
            varReference,
            this.stateNode.getFrameIdForScope(varReference)
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
    handleValueResponse(message: SFDAP.ValueResponse, _request: SFDAP.ValueRequest) {
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

    // TODO: move these of these to a seperate Source locator class
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
}
