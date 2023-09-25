import { DebugAdapter, DebugAdapterInlineImplementation, DebugProtocolMessage, Event, EventEmitter } from "vscode";
import { DebugProtocol as DAP } from "@vscode/debugprotocol";
import * as net from 'net';
import * as stream from "stream";
import { Emitter } from "vscode-languageclient";
import * as fs from 'fs'
import { StarfieldDebugProtocol as SFDAP } from "./StarfieldDebugProtocol";

function getFakeInitializeResponse(reqSeq: number) {
    return {
        type: 'response',
        seq: 0,
        request_seq: reqSeq,
        success: true,
        command: 'initialize',
        body: {
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
            supportsModulesRequest: false,
            additionalModuleColumns: [],
            supportedChecksumAlgorithms: [],
            supportsRestartRequest: false,
            supportsExceptionOptions: false,
            supportsValueFormattingOptions: false,
            supportsExceptionInfoRequest: false,
            supportTerminateDebuggee: false,
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
            completionTriggerCharacters: []
        }
    } as DAP.InitializeResponse;
}

const TWO_CRLF = '\r\n\r\n';
const HEADER_LINESEPARATOR = /\r?\n/;	// allow for non-RFC 2822 conforming line separators
const HEADER_FIELDSEPARATOR = /: */;

export class StarfieldDebugAdapterProxy implements DebugAdapter {
    private connected = false;
	private outputStream!: stream.Writable;
    private inputStream!: stream.Readable;
	private rawData = Buffer.allocUnsafe(0);
	private contentLength = -1;
    protected _socket?: net.Socket;
    protected port: number;
    protected host: string;
    protected readonly _onError = new Emitter<Error>();
    _onDidSendMessage = new EventEmitter<DebugProtocolMessage>()
    
    // object name to source map
    protected objectNameToSourceMap: Map<string, DAP.Source> = new Map<string, DAP.Source>();
    protected pathtoObjectNameMap: Map<string, string> = new Map<string, string>();



    constructor (port: number, host:string) {
        
        this.port = port;
        this.host = host;
        this.start();
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

    handleStackTraceResponse(message: SFDAP.StackTraceResponse) {
        // need to convert the source names back to Source objects
        if (message.body.stackFrames){
            message.body.stackFrames.forEach((frame: any)=>{
                if (frame.source){
                    let source = this.objectNameToSourceMap.get(frame.source)!;
                    frame.source = source;
                }
                frame.moduleId = frame.object;
            });
        }
        this.sendMessageToClient(message);
    }
    
    // TODO: this
    handleValueResponse(message: SFDAP.ValueResponse) {
        this.sendMessageToClient(message);
    }
    
    // TODO: this
    handleVariablesResponse(message: SFDAP.VariablesResponse) {
        this.sendMessageToClient(message);
    }


    // They set body.breakpoints[].source argument to a string instead of a source object, need to fix this
    handleSetBreakpointsResponse(message: SFDAP.SetBreakpointsResponse): void{
        if (message.body && message.body.breakpoints){
            message.body.breakpoints.forEach((breakpoint: any)=>{
                let source = this.objectNameToSourceMap.get(breakpoint.source)!;
                breakpoint.source = source;
            });
        }

        this.sendMessageToClient(message);
    }

    handleMessageFromServer(message: DAP.ProtocolMessage): void {

        if (message.type == "response") {
            const response = message as DAP.Response;
            if (response.command == "setBreakpoints") {
                this.handleSetBreakpointsResponse(response as SFDAP.SetBreakpointsResponse);
                return;
            } else if (response.command == "variables") {
                this.handleVariablesResponse(response as SFDAP.VariablesResponse);
                return;
            } else if (response.command == "value") {
                this.handleValueResponse(response as SFDAP.ValueResponse);
                return;
            } else if (response.command == "stackTrace") {
                this.handleStackTraceResponse(response as SFDAP.StackTraceResponse);
                return;
            }
        } else if (message.type == "event") {
            const event = message as DAP.Event;
            if (event.event == "output") {
                this.handleOutputEvent(event as SFDAP.OutputEvent);
                return;
            } else if (event.event == "version") {
                this.handleVersionEvent(event as SFDAP.VersionEvent);
                return;
            }
        }

        this.sendMessageToClient(message);

    }


    // takes in a Source object and returns the papyrus object idnetifier (e.g. "MyMod:MyScript")
    sourceToObjectName(source: DAP.Source) {
        let name = source.name || "";
        let path = source.path || "";

        let objectName: string = name.split(".")[0];

        // check the object name map path first
        if (this.pathtoObjectNameMap.has(path)) {
            objectName = this.pathtoObjectNameMap.get(path)!;
        } else if (path) {
            try {
                // Read the first line of the file to get the script name
                fs.readFileSync(path, 'utf8').split(/\r?\n/).forEach((line) => {
                    if (line.trim().toLowerCase().startsWith("scriptname")) {
                        objectName = line.trim().split(" ")[1];
                        return;
                    }
                });
            } catch (e) {
                console.log("Error reading file: " + e);
            }
            // set the object name map path
            this.pathtoObjectNameMap.set(path, objectName);
        }
        // set the object name to source map for retrieval later
        this.objectNameToSourceMap.set(objectName, source);
        return objectName;
    }



    handleSetBreakpointsRequest(message: DAP.SetBreakpointsRequest): void{
        let source = message.arguments.source;
        let objectName: string = this.sourceToObjectName(source);

        let mangled_message = message as any;
        mangled_message.arguments.source = objectName;
        this.sendMessageToServer(mangled_message);
    }
    
    handleVariablesRequest(message: DAP.VariablesRequest) {
        // TODO: SOMETHING?!
        this.sendMessageToServer(message);
    }

    async start() {
        this._socket = net.createConnection(this.port, this.host, () => {
            this.connect(this._socket!, this._socket!);
            this.connected = true;
        });
        this._socket.on('close', () => {
            if (this.connected) {
                this.connected = false;
                this.sendMessageToClient({
                    type: 'event',
                    event: 'exited',
                    body: {
                        exitCode: 0
                    }
                } as DAP.ExitedEvent)
                this._onError.fire(new Error('connection closed'));
            } else {
                throw new Error('connection closed');
            }
        });

        this._socket.on('error', error => {
            if (this.connected) {
                this.sendMessageToClient({
                    type: 'event',
                    event: 'exited',
                    body: {
                        exitCode: 1
                    }
                } as DAP.ExitedEvent)
                this._onError.fire(error);
            } else {
                throw error;
            }
        });

    }

	protected connect(readable: stream.Readable, writable: stream.Writable): void {

		this.outputStream = writable;
		this.rawData = Buffer.allocUnsafe(0);
		this.contentLength = -1;
        this.inputStream = readable;
		this.inputStream.on('data', (data: Buffer) => this.handleData(data));
	}


	private handleData(data: Buffer): void {

		this.rawData = Buffer.concat([this.rawData, data]);

		while (true) {
			if (this.contentLength >= 0) {
				if (this.rawData.length >= this.contentLength) {
					const message = this.rawData.toString('utf8', 0, this.contentLength);
					this.rawData = this.rawData.slice(this.contentLength);
					this.contentLength = -1;
					if (message.length > 0) {
						try {
                            this.handleMessageFromServer(JSON.parse(message));
						} catch (e) {
							console.error("Received invalid JSON message: ", message, e);
						}
					}
					continue;	// there may be more complete messages to process
				}
			} else {
				const idx = this.rawData.indexOf(TWO_CRLF);
				if (idx !== -1) {
					const header = this.rawData.toString('utf8', 0, idx);
					const lines = header.split(HEADER_LINESEPARATOR);
					for (const h of lines) {
						const kvPair = h.split(HEADER_FIELDSEPARATOR);
						if (kvPair[0] === 'Content-Length') {
							this.contentLength = Number(kvPair[1]);
						}
					}
					this.rawData = this.rawData.slice(idx + TWO_CRLF.length);
					continue;
				}
			}
			break;
		}
	}

    sendMessageToClient(message: DAP.ProtocolMessage): void {
        this._onDidSendMessage.fire(message as DebugProtocolMessage);
    }
    // Send message to server
    sendMessageToServer(message: SFDAP.ProtocolMessage): void {
        const json = JSON.stringify(message);
        this.outputStream.write(`Content-Length: ${Buffer.byteLength(json, 'utf8')}${TWO_CRLF}${json}`, 'utf8');
    }
    // override Debug Adapter
    
    // this is the event that the debug adapter client (i.e. vscode) will listen
    // to whenever we get a message from the server.
    onDidSendMessage = this._onDidSendMessage.event

    // handle message from client to server
    handleMessage (message: DebugProtocolMessage): void {
        let pmessage = message as DAP.ProtocolMessage
        if (this.outputStream) {
            if (pmessage.type === "request") {
                // Handling initialize/attach/launch requests that are REQUIRED by the DAP spec to be responded to,
                // But of course, Starfield doesn't respond to them, so we have to fake it
                // Or the debugger doesn't think it ever attached.
                let rmessage = message as DAP.Request;
                if (rmessage.command === "initialize"){
                    console.log("SENDING FAKE INITIALIZE RESPONSE BACK")
                    this.sendMessageToClient(getFakeInitializeResponse(rmessage.seq))
                    return; // Don't send message to server
                } else if (rmessage.command === "attach" || rmessage.command === "launch") {
                    console.log("SENDING FAKE attach/launch RESPONSE BACK")
                    let fakealResponse = {
                        type: "response",
                        request_seq: rmessage.seq,
                        success: true,
                        command: rmessage.command,
                    } as DAP.Response
                    this.sendMessageToClient(fakealResponse)
                    // Now we've attached/launched, we fire off the intialized event and we're off to the races
                    console.log("SENDING FAKE initialized event BACK")
                    this.sendMessageToClient({
                        type: 'event',
                        event: 'initialized'
                    }as DAP.InitializedEvent)
                    return; // Don't send message to server
                } else if (rmessage.command === "setBreakpoints") {
                    this.handleSetBreakpointsRequest(message as DAP.SetBreakpointsRequest);
                    return;
                } else if (rmessage.command === "variables") {
                    this.handleVariablesRequest(message as DAP.VariablesRequest);
                    return;
                }
                // TODO: Need to handle "threads" request that gets sent while attempting to pause 
                // The server returns an error response because starfield refuses to return any threads before the VM is paused
                // So no subsequent pause request is sent
                
                // TODO: Will need to handle "scope" requests, since starfield doesn't return any scopes
                // Translate the scopes into root/path in the custom variable requests and save them as a VariableReference?

                // TODO: need to handle "value" request?

            }
            this.sendMessageToServer(pmessage);
        }
    }

    dispose () {
        this._socket?.destroy();
    }
    

}