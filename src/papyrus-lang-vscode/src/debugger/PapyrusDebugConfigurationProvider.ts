import { DebugConfigurationProvider, CancellationToken, WorkspaceFolder, DebugConfiguration, debug } from 'vscode';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { LanguageClientConsumerBase } from '../common/LanguageClientConsumerBase';
import { Disposable } from 'vscode-jsonrpc';
import { IPapyrusDebugConfiguration } from './PapyrusDebugSession';

// TODO: Auto install F4SE plugin
// TODO: Warn if port is not open/if Fallout4.exe is not running

// Possibly based on custom language server requests:
// TODO: Resolve project from whichever that includes the active editor file.
// TODO: Provide configurations based on .ppj files in current directory.

export class PapyrusDebugConfigurationProvider implements DebugConfigurationProvider, Disposable {
    private readonly _registration: Disposable;

    constructor() {
        this._registration = debug.registerDebugConfigurationProvider('papyrus', this);
    }

    async provideDebugConfigurations(
        folder: WorkspaceFolder | undefined,
        token?: CancellationToken
    ): Promise<IPapyrusDebugConfiguration[]> {
        return [
            {
                type: 'papyrus',
                name: 'Fallout 4',
                request: 'attach',
                game: PapyrusGame.fallout4,
            },
        ];
    }

    // async resolveDebugConfiguration(
    //     folder: WorkspaceFolder | undefined,
    //     debugConfiguration: DebugConfiguration,
    //     token?: CancellationToken
    // ): Promise<DebugConfiguration> {
    //     return null;
    // }

    dispose() {
        this._registration.dispose();
    }
}
