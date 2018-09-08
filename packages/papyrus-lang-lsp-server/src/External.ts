// tslint:disable:variable-name

import { createDecorator } from 'decoration-ioc';
import { Connection, RemoteConsole, TextDocuments } from 'vscode-languageserver';

export const ITextDocuments = createDecorator<TextDocuments>('textDocuments');
export const ILanguageServerConnection = createDecorator<Connection>('languageServerConnection');
export const IRemoteConsole = createDecorator<RemoteConsole>('remoteConsole');
