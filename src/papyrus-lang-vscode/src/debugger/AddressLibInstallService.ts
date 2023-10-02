import { inject, injectable, interfaces } from 'inversify';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from '../PapyrusGame';
import { DownloadResult } from '../common/GithubHelpers';
import { CancellationToken, CancellationTokenSource } from 'vscode';

import * as AddLib from './AddLibHelpers';

export enum AddressLibDownloadedState {
    notDownloaded,
    latest,
    outdated,
    downloadedButCantCheckForUpdates,
}

export enum AddressLibInstalledState {
    notInstalled,
    installed,
    outdated,
    installedButCantCheckForUpdates,
}

export interface IAddressLibraryInstallService {
    getInstallState(game: PapyrusGame, modsDir?: string): Promise<AddressLibInstalledState>;
    getDownloadedState(): Promise<AddressLibDownloadedState>;
    DownloadLatestAddressLibs(cancellationToken?: CancellationToken): Promise<DownloadResult>;
    installLibraries(
        game: PapyrusGame,
        forceDownload: boolean,
        cancellationToken?: CancellationToken,
        modsDir?: string
    ): Promise<boolean>;
}

@injectable()
export class AddressLibraryInstallService implements IAddressLibraryInstallService {
    private readonly _pathResolver: IPathResolver;

    constructor(@inject(IPathResolver) pathResolver: IPathResolver) {
        this._pathResolver = pathResolver;
    }

    public async DownloadLatestAddressLibs(
        cancellationToken = new CancellationTokenSource().token
    ): Promise<DownloadResult> {
        const addressLibDownloadPath = await this._pathResolver.getAddressLibraryDownloadFolder();
        const addressLibDLJSONPath = await this._pathResolver.getAddressLibraryDownloadJSON();
        const status = await AddLib.DownloadLatestAddressLibs(
            addressLibDownloadPath,
            addressLibDLJSONPath,
            cancellationToken
        );
        return status;
    }

    private async getCurrentDownloadedAssetList(): Promise<AddLib.AddressLibReleaseAssetList | undefined> {
        const addressLibDLJSONPath = await this._pathResolver.getAddressLibraryDownloadJSON();
        return AddLib.GetAssetList(addressLibDLJSONPath);
    }

    private async checkDownloadIntegrity(): Promise<boolean> {
        const addressLibDownloadPath = await this._pathResolver.getAddressLibraryDownloadFolder();
        const assetList = await this.getCurrentDownloadedAssetList();
        if (!assetList) {
            return false;
        }
        return await AddLib._checkDownloadIntegrity(addressLibDownloadPath, assetList);
    }

    /**
     * Gets the state of the address library download
     * - If the address library is not downloaded or the download is corrupt, it will return `notDownloaded`
     * - If the address library is downloaded but the version can't be checked, it will return `downloadedButCantCheck`
     * - If the address library is downloaded but the version is outdated, it will return `outdated`
     * - If the address library is downloaded and the version is up to date, it will return `latest`
     * @returns AddressLibDownloadedState
     */
    public async getDownloadedState(): Promise<AddressLibDownloadedState> {
        // If it's not downloaded or the download is corrupt, we return notDownloaded
        if (!(await this.checkDownloadIntegrity())) {
            return AddressLibDownloadedState.notDownloaded;
        }
        const assetList = await this.getCurrentDownloadedAssetList();
        if (!assetList) {
            return AddressLibDownloadedState.notDownloaded;
        }
        // At this point, we know if SOME version is downloaded and is valid, but we don't know if it's the latest
        const latestReleaseInfo = await AddLib.getLatestAddLibReleaseInfo();
        if (!latestReleaseInfo) {
            return AddressLibDownloadedState.downloadedButCantCheckForUpdates;
        }
        const latestAssetList = AddLib.getAssetListFromAddLibRelease(latestReleaseInfo);
        if (!latestAssetList) {
            return AddressLibDownloadedState.downloadedButCantCheckForUpdates;
        }
        if (latestAssetList.version != assetList.version) {
            return AddressLibDownloadedState.outdated;
        }
        return AddressLibDownloadedState.latest;
    }

    /**
     * Right now, this just checks if the address libraries are installed or not
     * It returns either "Installed" or "Not Installed".
     * In the future, we might check if the installed address libraries are outdated or not
     * @param game
     * @param modsDir
     * @returns
     */
    public async getInstallState(game: PapyrusGame, modsDir?: string): Promise<AddressLibInstalledState> {
        // TODO: Verify this is what we want to do for Starfield
        if (game === PapyrusGame.starfield)
            return AddressLibInstalledState.installed;
    
        const ModsInstallDir = modsDir || (await this._pathResolver.getModParentPath(game)) || '';
        if (!ModsInstallDir || ModsInstallDir.length === 0) {
            return AddressLibInstalledState.notInstalled;
        }
        const state = await AddLib._checkAddressLibsInstalled(game, ModsInstallDir);
        if (state === AddressLibInstalledState.notInstalled) {
            return AddressLibInstalledState.notInstalled;
        }

        // At this point, we know if the address libraries are installed or not, but we don't know if they're outdated
        // TODO: For right now, we're not going to attempt to update the address libraries if they're outdated
        // We will have to have to ensure that the repo that we are using is always up-to-date before we start doing this
        return state;

        const downloadedState = await this.getDownloadedState();
        // We don't check the installed address lib versions if we don't have the latest version downloaded
        if (downloadedState !== AddressLibDownloadedState.latest) {
            return AddressLibInstalledState.installedButCantCheckForUpdates;
        }
        const assetList = await this.getCurrentDownloadedAssetList();
        if (!assetList) {
            return AddressLibInstalledState.installedButCantCheckForUpdates;
        }
        return await AddLib._checkAddressLibsInstalled(game, ModsInstallDir, assetList);
    }

    public async installLibraries(
        game: PapyrusGame,
        forceDownload: boolean = false,
        cancellationToken = new CancellationTokenSource().token,
        modsDir: string | undefined
    ): Promise<boolean> {
        const ParentInstallDir = modsDir || (await this._pathResolver.getModParentPath(game));
        if (!ParentInstallDir) {
            return false;
        }
        const addressLibDownloadPath = await this._pathResolver.getAddressLibraryDownloadFolder();
        const downloadedState = await this.getDownloadedState();
        if (downloadedState === AddressLibDownloadedState.notDownloaded) {
            if (forceDownload) {
                if ((await this.DownloadLatestAddressLibs(cancellationToken)) != DownloadResult.success) {
                    return false;
                }
            } else {
                return false;
            }
        }
        const assetList = await this.getCurrentDownloadedAssetList();

        if (!assetList) {
            return false;
        }
        return AddLib._installAddressLibs(game, ParentInstallDir, addressLibDownloadPath, assetList, cancellationToken);
    }
}

export const IAddressLibraryInstallService: interfaces.ServiceIdentifier<IAddressLibraryInstallService> =
    Symbol('addressLibraryInstallService');
