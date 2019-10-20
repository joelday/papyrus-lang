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
            const imports = config.importDirs;
            return '"' + imports.join(';') + '"';
        });

        this._sourcesCommand = commands.registerCommand("papyrus.projectSources", () => {
            const sources = config.sourceDirs;
            return sources.map((e) => '"' + e + '"').join(' ');
        });
    }

    dispose() {
        this._importsCommand.dispose();
        this._sourcesCommand.dispose();
    }
}