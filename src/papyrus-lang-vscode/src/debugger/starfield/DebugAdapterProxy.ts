import { DebugProtocol as DAP } from "@vscode/debugprotocol";
import * as net from 'net';
import * as stream from "stream";
import * as fs from 'fs'
import { StarfieldDebugProtocol as SFDAP } from "./StarfieldDebugProtocol";
import { ProtocolServer } from "@vscode/debugadapter/lib/protocol";


interface DebugProtocolMessage {
}

interface IDisposable {
	dispose(): void;
}

class Disposable0 implements IDisposable {
	dispose(): any {
	}
}

interface Event0<T> {
	(listener: (e: T) => any, thisArg?: any): Disposable0;
}

class Emitter<T> {

	private _event?: Event0<T>;
	private _listener?: (e: T) => void;
	private _this?: any;

	get event(): Event0<T> {
		if (!this._event) {
			this._event = (listener: (e: T) => any, thisArg?: any) => {

				this._listener = listener;
				this._this = thisArg;

				let result: IDisposable;
				result = {
					dispose: () => {
						this._listener = undefined;
						this._this = undefined;
					}
				};
				return result;
			};
		}
		return this._event;
	}

	fire(event: T): void {
		if (this._listener) {
			try {
				this._listener.call(this._this, event);
			} catch (e) {
			}
		}
	}

	hasListener() : boolean {
		return !!this._listener;
	}

	dispose() {
		this._listener = undefined;
		this._this = undefined;
	}
}

/**
 * A structurally equivalent copy of vscode.DebugAdapter
 */
interface VSCodeDebugAdapter extends Disposable0 {

	readonly onDidSendMessage: Event0<DebugProtocolMessage>;

	handleMessage(message: DAP.ProtocolMessage): void;
}

const TWO_CRLF = '\r\n\r\n';
const HEADER_LINESEPARATOR = /\r?\n/;	// allow for non-RFC 2822 conforming line separators
const HEADER_FIELDSEPARATOR = /: */;

export abstract class DebugAdapterProxy implements VSCodeDebugAdapter {
	protected logfile: fs.WriteStream
    protected connected = false;
	protected outputStream!: stream.Writable;
    protected inputStream!: stream.Readable;
	protected rawData = Buffer.allocUnsafe(0);
	protected contentLength = -1;
    protected _socket?: net.Socket;
    protected port: number;
    protected host: string;
    protected readonly _onError = new Emitter<Error>();
    protected _sendMessage = new Emitter<DebugProtocolMessage>()


    constructor (port: number, host:string, startNow: boolean = true) {
        
        this.port = port;
        this.host = host;
        if (startNow)
            this.start();
		// TODO: make this configurable
		let basedir = "C:\\Users\\Nikita\\Documents\\My Games\\Starfield\\Logs\\";
		let logfile = basedir + "debugadapter.log";
		this.logfile = fs.createWriteStream(logfile, { flags: 'w' });
    }
	public logerror(message: string, ...args: any[]) {
		console.error(message, ...args);
		this.logfile.write(`ERROR: ${message} ${args.join(' ')}`);

	}
    public log(message: string, ...args: any[]){
		console.log(message, ...args);
		this.logfile.write(`${message} ${args.join(' ')}`);
	}

    public start() {
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
                this.connected = false;
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

    public stop(){
        this._socket?.destroy();
    }

    //override this
    protected handleMessageFromServer?(message: DAP.ProtocolMessage): void
    
    //override this
    protected handleMessageFromClient?(message: DAP.ProtocolMessage): void
    
    protected sendMessageToClient(message: DAP.ProtocolMessage): void {
		this.log(`***PROXY->CLIENT: ${JSON.stringify(message, undefined, 2)}`);

        this._sendMessage.fire(message as DebugProtocolMessage);
    }
    // Send message to server
    protected sendMessageToServer(message: DAP.ProtocolMessage): void {
		this.log(`***PROXY->SERVER ${JSON.stringify(message, undefined, 2)}`);

        if (this.outputStream){
            const json = JSON.stringify(message);
            this.outputStream.write(`Content-Length: ${Buffer.byteLength(json, 'utf8')}${TWO_CRLF}${json}`, 'utf8');    
        }
    }



	protected connect(readable: stream.Readable, writable: stream.Writable): void {

		this.outputStream = writable;
		this.rawData = Buffer.allocUnsafe(0);
		this.contentLength = -1;
        this.inputStream = readable;
		this.inputStream.on('data', (data: Buffer) => this.handleData(data));
	}


	protected handleData(data: Buffer): void {

		this.rawData = Buffer.concat([this.rawData, data]);

		while (true) {
			if (this.contentLength >= 0) {
				if (this.rawData.length >= this.contentLength) {
					const message = this.rawData.toString('utf8', 0, this.contentLength);
					this.rawData = this.rawData.slice(this.contentLength);
					this.contentLength = -1;
					if (message.length > 0) {
                        if (!this.handleMessageFromServer) {
                            throw new Error("handleMessageFromServer is undefined");
                        }
						try {
                            this.handleMessageFromServer(JSON.parse(message));
						} catch (e) {
							this.logerror("Received invalid JSON message: ", message, e);
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


    protected _isRunningInline() {
		return this._sendMessage && this._sendMessage.hasListener();
	}

	// ---- implements vscode.Debugadapter interface ---------------------------
    
    // this is the event that the debug adapter client (i.e. vscode) will listen
    // to whenever we get a message from the server.
    public onDidSendMessage = this._sendMessage.event

    // handle message from client to server
    public handleMessage (message: DebugProtocolMessage): void {
        if (!this.handleMessageFromClient){
            throw new Error("handleMessageFromClient is undefined");
        }
        this.handleMessageFromClient(message as DAP.ProtocolMessage)
    }

    public dispose () {
        this._socket?.destroy();
    }



}