import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { take } from 'rxjs/operators';
import { ClientHostStatus } from '../server/LanguageClientHost';

export abstract class LanguageClientConsumerBase {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _game: PapyrusGame;

    constructor(languageClientManager: ILanguageClientManager, game: PapyrusGame) {
        this._languageClientManager = languageClientManager;
        this._game = game;
    }

    protected async getLanguageClientHost() {
        return await this._languageClientManager.clients
            .get(this._game)
            .pipe(take(1))
            .toPromise();
    }

    protected async getLanguageClient() {
        const clientHost = await this.getLanguageClientHost();

        const status = await clientHost.status.pipe(take(1)).toPromise();
        if (status !== ClientHostStatus.running) {
            return null;
        }

        return clientHost.client;
    }
}
