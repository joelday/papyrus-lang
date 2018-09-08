import { createDecorator } from 'decoration-ioc';
import { Connection, RemoteConsole } from 'vscode-languageserver';

// tslint:disable-next-line:variable-name
export const ILanguageServerConnection = createDecorator<Connection>('languageServerConnection');

// tslint:disable-next-line:variable-name
export const IRemoteConsole = createDecorator<RemoteConsole>('remoteConsole');
