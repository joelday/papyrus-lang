import { commands, Disposable } from 'vscode';
import { IExtensionConfigProvider } from '../../ExtensionConfigProvider';
import { take } from 'rxjs/operators';

export class SubstitutionCommands implements Disposable {
    private readonly _configProvider: IExtensionConfigProvider;
    private _importsCommand: Disposable;
    private _sourcesCommand: Disposable;

    constructor(@IExtensionConfigProvider configProvider: IExtensionConfigProvider) {
        this._configProvider = configProvider;
        this.registerCommands();
    }

    private async registerCommands() {
        const config = (await this._configProvider.config.pipe(take(1)).toPromise())['skyrimSpecialEdition'];

        this._importsCommand = commands.registerCommand("papyrus.projectImports", () => {
            return config.importDirs.join(';').replace('${installPath}', config.installPath);
        });

        this._sourcesCommand = commands.registerCommand("papyrus.projectSources", () => {
            return config.sourceDirs.join(' ').replace('${installPath}', config.installPath);
        });
    }

    dispose() {
        this._importsCommand.dispose();
        this._sourcesCommand.dispose();
    }
}