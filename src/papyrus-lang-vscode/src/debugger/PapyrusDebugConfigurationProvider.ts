import { DebugConfigurationProvider, CancellationToken, WorkspaceFolder, DebugConfiguration } from 'vscode';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { LanguageClientConsumerBase } from '../common/LanguageClientConsumerBase';

export class PapyrusDebugConfigurationProvider extends LanguageClientConsumerBase
    implements DebugConfigurationProvider {
    constructor(@ILanguageClientManager languageClientManager: ILanguageClientManager) {
        super(languageClientManager, PapyrusGame.fallout4);
    }

    async provideDebugConfigurations(
        folder: WorkspaceFolder | undefined,
        token?: CancellationToken
    ): Promise<DebugConfiguration[]> {
        const client = await this.getLanguageClient();

        return null;
    }

    async resolveDebugConfiguration(
        folder: WorkspaceFolder | undefined,
        debugConfiguration: DebugConfiguration,
        token?: CancellationToken
    ): Promise<DebugConfiguration> {
        return null;
    }
}
