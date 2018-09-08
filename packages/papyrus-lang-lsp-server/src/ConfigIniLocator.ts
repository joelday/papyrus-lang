import { CreationKitIniLocations, ICreationKitIniLocator } from 'papyrus-lang/lib/config/CreationKitIniLocator';
import * as path from 'upath';
import URI from 'vscode-uri';
import { IConfigProvider } from './ConfigProvider';

export class ConfigIniLocator implements ICreationKitIniLocator {
    private readonly _configProvider: IConfigProvider;

    constructor(@IConfigProvider configProvider: IConfigProvider) {
        this._configProvider = configProvider;
    }

    public getIniLocations(workspaceUri: string): CreationKitIniLocations {
        const config = this._configProvider.config;

        if (!config || !config.fallout4) {
            return null;
        }

        let installPath = config.fallout4.installPath ? path.normalizeSafe(config.fallout4.installPath) : null;

        if (!path.isAbsolute(installPath)) {
            installPath = path.resolve(path.join(URI.parse(workspaceUri).fsPath, installPath));
        }

        return {
            creationKitInstallUri: URI.file(installPath).toString(),
            iniUris: config.fallout4.creationKitIniFiles
                ? config.fallout4.creationKitIniFiles.map((iniPath) =>
                      URI.file(path.resolve(path.join(installPath, path.normalizeSafe(iniPath)))).toString()
                  )
                : [],
        };
    }
}
