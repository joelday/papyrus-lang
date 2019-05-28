import {
    ProviderResult,
    DebugAdapterDescriptorFactory,
    DebugSession,
    DebugAdapterExecutable,
    DebugAdapterDescriptor,
} from 'vscode';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { LanguageClientConsumerBase } from '../common/LanguageClientConsumerBase';

export class PapyrusDebugAdapterDescriptorFactory extends LanguageClientConsumerBase
    implements DebugAdapterDescriptorFactory {
    constructor(@ILanguageClientManager languageClientManager: ILanguageClientManager) {
        super(languageClientManager, PapyrusGame.fallout4);
    }

    createDebugAdapterDescriptor(
        session: DebugSession,
        executable: DebugAdapterExecutable
    ): ProviderResult<DebugAdapterDescriptor> {
        return null;
    }
}
