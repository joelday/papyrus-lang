import { createDecorator } from 'decoration-ioc';
import { Connection } from 'vscode-languageserver';

// tslint:disable-next-line:variable-name
export const ILanguageServerConnection = createDecorator<Connection>(
    'languageServerConnection'
);
