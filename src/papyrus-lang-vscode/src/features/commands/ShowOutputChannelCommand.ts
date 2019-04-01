import { CommandBase } from '../../common/vscode/commands/CommandBase';
import { OutputChannel } from 'vscode';

export class ShowOutputChannelCommand extends CommandBase {
    private readonly _outputChannelProvider: () => OutputChannel;

    constructor(uniqueId: string, outputChannelProvider: () => OutputChannel) {
        super(`papyrus.showOutputChannel.${uniqueId}`);
        this._outputChannelProvider = outputChannelProvider;
    }

    onExecute() {
        this._outputChannelProvider()!.show(true);
    }
}