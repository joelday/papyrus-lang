import { createDecorator } from 'decoration-ioc';

export interface ScriptText {
    text: string;
    version: string;
}

export interface IScriptTextProvider {
    getScriptText(uri: string): ScriptText;
    getScriptVersion(uri: string): string;
}

// tslint:disable-next-line:variable-name
export const IScriptTextProvider = createDecorator<IScriptTextProvider>(
    'scriptTextProvider'
);
