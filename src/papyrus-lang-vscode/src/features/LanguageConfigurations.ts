import { Disposable } from 'vscode-jsonrpc';
import { languages } from 'vscode';

export class LanguageConfigurations {
    private _languageConfigurationHandle: Disposable;

    constructor() {
        this._languageConfigurationHandle = languages.setLanguageConfiguration('papyrus', {
            comments: {
                lineComment: ';',
                blockComment: [';/', '/;'],
            },
            brackets: [['{', '}'], ['[', ']'], ['(', ')']],
            indentationRules: {
                increaseIndentPattern: /^\s*(if|(\S+\s+)?(property\W+\w+(?!.*(auto)))|struct|group|state|event|(\S+\s+)?(function.*\(.*\)(?!.*native))|else|elseif)/i,
                decreaseIndentPattern: /^\s*(endif|endproperty|endstruct|endgroup|endstate|endevent|endfunction|else|elseif)/i,
            },
        });
    }

    dispose() {
        this._languageConfigurationHandle.dispose();
    }
}
