import { CommandBase } from '../../common/vscode/commands/CommandBase';
import { OutputChannel } from 'vscode';

export class ShowOutputChannelCommand extends CommandBase {
    private readonly _outputChannelProvider: () => OutputChannel | null;

    constructor(uniqueId: string, outputChannelProvider: () => OutputChannel | null) {
        super(`papyrus.showOutputChannel.${uniqueId}`);
        this._outputChannelProvider = outputChannelProvider;
    }

    onExecute() {
        this._outputChannelProvider()?.show(true);
    }
}
