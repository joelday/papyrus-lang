import { Disposable, ExtensionContext } from 'vscode';
import { ServiceCollection, IInstantiationService, InstantiationService, Descriptor } from 'decoration-ioc';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { IExtensionConfigProvider, ExtensionConfigProvider } from './ExtensionConfigProvider';
import { LanguageClientManager } from './server/LanguageClientManager';
import * as vscode from 'vscode';

class PapyrusExtension implements Disposable {
    private readonly _context: ExtensionContext;
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;
    private readonly _clientManager: LanguageClientManager;
    private readonly _languageConfigurationHandle: Disposable;

    constructor(context: ExtensionContext) {
        this._context = context;

        this._serviceCollection = new ServiceCollection(
            [IExtensionContext, context],
            [IExtensionConfigProvider, new Descriptor(ExtensionConfigProvider)]
        );

        this._instantiationService = new InstantiationService(this._serviceCollection);

        this._clientManager = this._instantiationService.createInstance(LanguageClientManager);

        this._languageConfigurationHandle = vscode.languages.setLanguageConfiguration('papyrus', {
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

        this._clientManager.dispose();
    }
}

export async function activate(context: ExtensionContext) {
    const extension = new PapyrusExtension(context);
    context.subscriptions.push(extension);
}
