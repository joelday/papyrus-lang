import { RequestHandler } from 'vscode-jsonrpc';
import { Connection, TextDocumentPositionParams } from 'vscode-languageserver';
import { FnArgs } from './Types';

export interface Handler<T extends keyof ConnectionFns> {
    readonly handleRequest: HandlerFn<T>;
}

export type ConnectionFns = Record<keyof Connection, Function>;
export type HandlerFn<T extends keyof ConnectionFns> = FnArgs<Connection[T]>[0];
export function registerHandler<T extends keyof ConnectionFns>(
    connection: Connection,
    registerMethod: T,
    handler: Handler<T>
) {
    (connection[registerMethod] as ConnectionFns[T])(handler);
}
