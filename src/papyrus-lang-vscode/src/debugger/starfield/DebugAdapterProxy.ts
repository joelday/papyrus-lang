/* eslint-disable no-prototype-builtins */
/* eslint-disable @typescript-eslint/no-explicit-any */
process.env.FORCE_COLOR = '2';

import { DebugProtocol as DAP } from '@vscode/debugprotocol';
import * as net from 'net';
import * as stream from 'stream';
import * as fs from 'fs';
import * as path from 'path';
import { pino } from 'pino';
import * as pino_pretty from 'pino-pretty';
import * as chalk_d from 'chalk';
import { default as colorizer } from '../../common/colorizer';
import { default as split } from 'split2';
import { Event } from '@vscode/debugadapter';
import * as url from 'url';

const chalk: chalk_d.Chalk = new chalk_d.default.constructor({ enabled: true, level: 2 });

interface DebugProtocolMessage {}

interface IDisposable {
    dispose(): void;
}

class Disposable0 implements IDisposable {
    dispose(): any {}
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

                const result: IDisposable = {
                    dispose: () => {
                        this._listener = undefined;
                        this._this = undefined;
                    },
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
                /* empty */
            }
        }
    }

    hasListener(): boolean {
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
const HEADER_LINESEPARATOR = /\r?\n/; // allow for non-RFC 2822 conforming line separators
const HEADER_FIELDSEPARATOR = /: */;

const custom_colors = {
    STRING_KEY: '#9cdcfe',
    STRING_LITERAL: '#CE9178',
    COLON: 'gray',
};

export function colorize_message(value: any) {
    let colorized = colorizer(value, { colors: custom_colors, pretty: true, forceColor: true });
    // Make the "success" and "message" fields red if they are present and "success" is false
    if (typeof value === 'object' && value !== null && value.hasOwnProperty('success') && !value.success) {
        const lines = colorized.split('\n');
        for (const idx in lines) {
            const line = lines[idx];
            if (line.includes('"success"') || line.includes('"message"')) {
                // eslint-disable-next-line no-control-regex
                const line_split = line.replace(/\x1b\[[0-9;]*m/g, '').split(':');
                const key = line_split[0];
                const value = line_split[1];
                const colorized_value = chalk.keyword('red')(value);
                const colorized_key = chalk.hex('#9cdcfe')(key);
                const colorized_colon = chalk.keyword('grey')(':');
                const result = `${colorized_key}${colorized_colon}${colorized_value}`;
                lines[idx] = result;
            }
        }
        colorized = lines.join('\n');
    }

    return colorized;
}
export type DAPLogLevel = pino.LevelWithSilent;

export interface DebugAdapterProxyOptions {
    port: number;
    host: string;
    /**
     * If true, start the proxy immediately (default: true)
     */
    startNow?: boolean;
    /**
     *  Log level for messages output to the console (default: "info")
     */
    consoleLogLevel?: DAPLogLevel;
    /**
     * Log level for messages output to the log file (default: "trace")
     * `quiet` turns off file logging
     */
    fileLogLevel?: DAPLogLevel;
    /**
     * Directory to write logs to (default: ~/.DAPProxy)
     */
    logdir?: string;
    /**
     * Log level for messages from the client to the proxy (default: "debug")
     */
    logClientToProxy?: DAPLogLevel;
    /**
     * Log level for messages from the proxy to the client (default: "trace")
     */
    logProxyToClient?: DAPLogLevel;
    /**
     * Log level for messages from the server to the proxy (default: "debug")
     */
    logServerToProxy?: DAPLogLevel;
    /**
     * Log level for messages from the proxy to the server (default: "trace")
     */
    logProxyToServer?: DAPLogLevel;

    /**
     * The client capabilities
     * default:
     * {
     *   adapterID: '',
     *   linesStartAt1: true,
     *   columnsStartAt1: true,
     *   pathFormat: 'path',
     *   supportsVariableType: true
     *   pathsAreURIs: false
     * }
     *
     */
    clientCapabilities?: ClientCapabilities;

    /**
     * The debugger locale
     * default:
     * {
     *  linesStartAt1: true,
     *  columnsStartAt1: true,
     *  pathsAreURIs: false
     * }
     */
    debuggerLocale?: DebuggerLocale;
}

// these are the ones we care about, so we make them mandatory
export interface ClientCapabilities extends DAP.InitializeRequestArguments {
    linesStartAt1: boolean;
    columnsStartAt1: boolean;
    pathFormat: 'path' | 'uri' | string;
    supportsVariableType: boolean;
    pathsAreURIs: boolean;
}

export interface DebuggerLocale {
    linesStartAt1: boolean;
    columnsStartAt1: boolean;
    pathsAreURIs: boolean;
}

const DEFAULT_CLIENT_CAPABILITIES: ClientCapabilities = {
    adapterID: 'DebugAdapterProxy',
    linesStartAt1: true,
    columnsStartAt1: true,
    pathFormat: 'path',
    supportsVariableType: true,
    pathsAreURIs: false,
};

export abstract class DebugAdapterProxy implements VSCodeDebugAdapter {
    protected connected = false;
    protected outputStream!: stream.Writable;
    protected inputStream!: stream.Readable;
    protected rawData = Buffer.allocUnsafe(0);
    protected contentLength = -1;
    protected _socket?: net.Socket;
    protected port: number;
    protected host: string;
    protected readonly _onError = new Emitter<Error>();
    protected _sendMessage = new Emitter<DebugProtocolMessage>();
    protected logClientToProxy: DAPLogLevel = 'debug';
    protected logProxyToClient: DAPLogLevel = 'trace';
    protected logServerToProxy: DAPLogLevel = 'debug';
    protected logProxyToServer: DAPLogLevel = 'trace';
    protected consoleLogLevel: DAPLogLevel = 'info';
    protected fileLogLevel: DAPLogLevel = 'trace';
    protected readonly connectionTimeoutLimit = 12000; // the debugger will disconnect after about 20 seconds, so we need to be faster than that
    protected connectionTimeout: NodeJS.Timeout | undefined;
    protected logDirectory: string;
    protected logFilePath: string;
    protected serverMsgQueue: DAP.ProtocolMessage[] = [];
    protected loggerFile: pino.Logger;
    protected loggerConsole: pino.Logger;
    protected logFile: stream.Writable;
    protected readonly logStream: stream.PassThrough;
    protected clientCaps: ClientCapabilities;
    protected debuggerLocale: DebuggerLocale;
    constructor(options: DebugAdapterProxyOptions) {
        this.port = options.port;
        this.host = options.host;
        this.consoleLogLevel = options.consoleLogLevel || this.consoleLogLevel;
        this.fileLogLevel = options.fileLogLevel || this.fileLogLevel;
        this.logClientToProxy = options.logClientToProxy || this.logClientToProxy;
        this.logProxyToClient = options.logProxyToClient || this.logProxyToClient;
        this.logServerToProxy = options.logServerToProxy || this.logServerToProxy;
        this.logProxyToServer = options.logProxyToServer || this.logProxyToServer;
        this.clientCaps = options.clientCapabilities || DEFAULT_CLIENT_CAPABILITIES;
        this.debuggerLocale = options.debuggerLocale || {
            linesStartAt1: true,
            columnsStartAt1: true,
            pathsAreURIs: false,
        };
        const homepath = process.env.HOME;
        this.logDirectory = options.logdir || path.join(homepath!, '.DAPProxy');
        this.logFilePath = this.getLogFilePath(this.logDirectory);
        this.logFile = fs.createWriteStream(this.logFilePath, { flags: 'w' });
        const pprinterFile = pino_pretty.default({
            colorize: false,
            ignore: 'pid,hostname',
            destination: this.logFilePath,
        });
        this.logStream = split((data: any) => {
            //sink()
            console.log(data);
            return;
            // return data;
        });
        const ppConsoleOpts = {
            colorize: true,
            colorizeObjects: true,
            customPrettifiers: {
                message: (value: any) => {
                    return colorize_message(value);
                },
            },
            ignore: 'pid,hostname',
            destination: this.logStream,
        };
        const pprinterConsole = pino_pretty.default(ppConsoleOpts);
        this.loggerFile = pino({ level: this.fileLogLevel }, pprinterFile);
        this.loggerConsole = pino({ level: this.consoleLogLevel }, pprinterConsole);
        if (options.startNow) this.start();
        this.loginfo('Started.');
    }

    protected getLogFilePath(logDir: string) {
        const date = new Date();
        return path.join(
            logDir,
            `debugadapter-${date.getFullYear()}-${date.getMonth()}-${date.getDate()}__${date.getHours()}-${date.getMinutes()}-${date.getSeconds()}.log`
        );
    }
    public logfatal(message: any, ...args: any[]) {
        this.log('fatal', message, ...args);
    }
    public logwarn(message: any, ...args: any[]) {
        this.log('warn', message, ...args);
    }
    public logerror(message: any, ...args: any[]) {
        this.log('error', message, ...args);
    }
    public loginfo(message: any, ...args: any[]) {
        this.log('info', message, ...args);
    }
    public logtrace(message: any, ...args: any[]) {
        this.log('trace', message, ...args);
    }
    public logdebug(message: any, ...args: any[]) {
        this.log('debug', message, ...args);
    }
    public log(level: DAPLogLevel, message: any, ...args: any[]) {
        switch (level) {
            case 'silent':
                break;
            case 'fatal':
                this.loggerFile.fatal(message, ...args);
                this.loggerConsole.fatal(message, ...args);
                break;
            case 'error':
                this.loggerFile.error(message, ...args);
                this.loggerConsole.error(message, ...args);
                break;
            case 'warn':
                this.loggerFile.warn(message, ...args);
                this.loggerConsole.warn(message, ...args);
                break;
            case 'info':
                this.loggerFile.info(message, ...args);
                this.loggerConsole.info(message, ...args);
                break;
            case 'debug':
                this.loggerFile.debug(message, ...args);
                this.loggerConsole.debug(message, ...args);
                break;
            case 'trace':
                this.loggerFile.trace(message, ...args);
                this.loggerConsole.trace(message, ...args);
                break;
            default:
                break;
        }
    }

    public start() {
        // set a timeout that kills the server if no connection is established within 12 seconds
        this.connectionTimeout = setTimeout(() => {
            this._onError.fire(new Error(`annot connect to Starfield DAP server`));
            this.emitOutputEvent('Cannot connect to Starfield DAP server!', 'important');
            this.stop();
        }, this.connectionTimeoutLimit);
        this._socket = net.createConnection(this.port, this.host, () => {
            this.connect(this._socket!, this._socket!);
            this.connected = true;
            clearTimeout(this.connectionTimeout);
        });
        this._socket.on('close', () => {
            if (this.connected) {
                this.emitOutputEvent('Connection closed!', 'important');
                this.stop();
                this._onError.fire(new Error('connection closed'));
            } else {
                throw new Error('connection closed');
            }
        });

        this._socket.on('error', (error) => {
            if (this.connected) {
                this.emitOutputEvent('Connection error!', 'important');
                this.stop();
                this._onError.fire(error);
            } else {
                throw error;
            }
        });
    }

    public stop() {
        this.connected = false;
        this.sendMessageToClient(new Event('terminated'));
        this._socket?.destroy();
    }

    protected emitOutputEvent(message: string, category: string = 'console') {
        const event = <DAP.OutputEvent>new Event('output');
        event.body = {
            category: category,
            output: message,
        };
        this.sendMessageToClient(event);
    }
    //override this
    protected handleMessageFromServer?(message: DAP.ProtocolMessage): void;

    //override this
    protected handleMessageFromClient?(message: DAP.ProtocolMessage): void;

    protected sendMessageToClient(message: DAP.ProtocolMessage, noLog: boolean = false): void {
        if (!noLog) {
            if (message.type === 'response' && !(message as DAP.Response).success) {
                this.log('warn', { message }, '***PROXY->CLIENT FAILED RESPONSE:');
            } else {
                this.log(this.logProxyToClient, { message }, '***PROXY->CLIENT:');
            }
        }
        this._sendMessage.fire(message as DebugProtocolMessage);
    }

    protected processServerMsgQueue() {
        while (this.serverMsgQueue.length > 0) {
            const msg = this.serverMsgQueue.shift();
            if (msg) {
                this.sendMessageToServer(msg);
            }
        }
    }

    // Send message to server
    protected sendMessageToServer(message: DAP.ProtocolMessage, nolog: boolean = false): void {
        if (!nolog) {
            this.log(this.logProxyToServer, { message }, '***PROXY->SERVER:');
        }
        if (!this.outputStream) {
            this.serverMsgQueue.push(message);
            if (this.serverMsgQueue.length == 1) {
                this._socket?.once('connect', () => {
                    this.processServerMsgQueue();
                });
            }
            return;
        }
        if (this.outputStream) {
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

        // eslint-disable-next-line no-constant-condition
        while (true) {
            if (this.contentLength >= 0) {
                if (this.rawData.length >= this.contentLength) {
                    const msgData = this.rawData.toString('utf8', 0, this.contentLength);
                    this.rawData = this.rawData.slice(this.contentLength);
                    this.contentLength = -1;
                    if (msgData.length > 0) {
                        if (!this.handleMessageFromServer) {
                            throw new Error('handleMessageFromServer is undefined');
                        }
                        try {
                            const message = JSON.parse(msgData);
                            this.log(this.logServerToProxy, { message }, '---SERVER->PROXY:');
                            try {
                                this.handleMessageFromServer(message);
                            } catch (e) {
                                this.logerror(e, `Error handling message from server: ${message}`);
                            }
                        } catch (e) {
                            this.logerror(e, `Received invalid JSON message: ${msgData}`);
                        }
                    }
                    continue; // there may be more complete messages to process
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

    protected setClientCapabilities(args: DAP.InitializeRequestArguments) {
        const nargs = args as ClientCapabilities;
        if (typeof args.adapterID !== 'string') {
            nargs.adapterID = DEFAULT_CLIENT_CAPABILITIES.adapterID;
        }
        if (typeof args.linesStartAt1 !== 'boolean') {
            nargs.linesStartAt1 = DEFAULT_CLIENT_CAPABILITIES.linesStartAt1;
        }
        if (typeof args.columnsStartAt1 !== 'boolean') {
            nargs.columnsStartAt1 = DEFAULT_CLIENT_CAPABILITIES.columnsStartAt1;
        }
        if (typeof args.pathFormat !== 'string') {
            nargs.pathsAreURIs = DEFAULT_CLIENT_CAPABILITIES.pathsAreURIs;
            nargs.pathFormat = DEFAULT_CLIENT_CAPABILITIES.pathFormat;
        } else {
            nargs.pathsAreURIs = args.pathFormat === 'uri';
        }
        if (typeof args.supportsVariableType !== 'boolean') {
            nargs.columnsStartAt1 = DEFAULT_CLIENT_CAPABILITIES.supportsVariableType;
        }
        // set the rest
        this.clientCaps = nargs;
    }

    // formatting
    protected static _formatPIIRegexp = /{([^}]+)}/g;
    protected static formatPII(format: string, excludePII: boolean, args: { [key: string]: string }): string {
        return format.replace(DebugAdapterProxy._formatPIIRegexp, function (match, paramName) {
            if (excludePII && paramName.length > 0 && paramName[0] !== '_') {
                return match;
            }
            return args[paramName] && args.hasOwnProperty(paramName) ? args[paramName] : match;
        });
    }

    convertClientLineToDebugger(line: number) {
        if (this.debuggerLocale.linesStartAt1) {
            return this.clientCaps.linesStartAt1 ? line : line + 1;
        }
        return this.clientCaps.linesStartAt1 ? line - 1 : line;
    }
    convertDebuggerLineToClient(line: number) {
        if (this.debuggerLocale.linesStartAt1) {
            return this.clientCaps.linesStartAt1 ? line : line - 1;
        }
        return this.clientCaps.linesStartAt1 ? line + 1 : line;
    }
    convertClientColumnToDebugger(column: number) {
        if (this.debuggerLocale.columnsStartAt1) {
            return this.clientCaps.columnsStartAt1 ? column : column + 1;
        }
        return this.clientCaps.columnsStartAt1 ? column - 1 : column;
    }
    convertDebuggerColumnToClient(column: number) {
        if (this.debuggerLocale.columnsStartAt1) {
            return this.clientCaps.columnsStartAt1 ? column : column - 1;
        }
        return this.clientCaps.columnsStartAt1 ? column + 1 : column;
    }

    convertClientPathToDebugger(clientPath: string) {
        if (this.clientCaps.pathsAreURIs !== this.debuggerLocale.pathsAreURIs) {
            if (this.clientCaps.pathsAreURIs) {
                return this.uri2path(clientPath);
            } else {
                return this.path2uri(clientPath);
            }
        }
        return clientPath;
    }

    convertDebuggerPathToClient(debuggerPath: string) {
        if (this.debuggerLocale.pathsAreURIs !== this.clientCaps.pathsAreURIs) {
            if (this.debuggerLocale.pathsAreURIs) {
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

    // ---- implements vscode.Debugadapter interface ---------------------------

    // this is the event that the debug adapter client (i.e. vscode) will listen
    // to whenever we get a message from the server.
    public onDidSendMessage = this._sendMessage.event;

    // handle message from client to server
    public handleMessage(message: DebugProtocolMessage): void {
        if (!this.handleMessageFromClient) {
            throw new Error('handleMessageFromClient is undefined');
        }
        this.log(this.logClientToProxy, { message }, '---CLIENT->PROXY:');
        this.handleMessageFromClient(message as DAP.ProtocolMessage);
    }

    public dispose() {
        this._socket?.destroy();
    }
}
