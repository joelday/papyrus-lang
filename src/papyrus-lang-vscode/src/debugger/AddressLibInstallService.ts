import { inject, injectable, interfaces } from 'inversify';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { CancellationToken, CancellationTokenSource } from 'vscode';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';

import { mkdirIfNeeded } from '../Utilities';

import { GithubRelease } from '@terascope/fetch-github-release/dist/src/interfaces';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

import md5File from 'md5-file';
import {
    AddressLibraryName,
    AddressLibAssetSuffix,
    getAddressLibNameFromAssetSuffix,
    getAsssetLibraryDLSuffix,
} from '../common/constants';
import extractZip from 'extract-zip';
import {
    CheckHashFile,
    DownloadAssetAndCheckHash,
    downloadAssetFromGitHub,
    DownloadResult,
    GetHashOfFolder,
    GetLatestReleaseFromRepo,
} from '../common/GithubHelpers';
import { getAddressLibNames } from './MO2Helpers';
const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);
const removeFile = promisify(fs.unlink);

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
    installedButCantCheckForUpdates
}

export interface Asset {
  /**
   * The file name of the zip file
   */
    zipFile: string;
    folderName: AddressLibraryName;
    zipFileHash: string;
    /**
     * For checking if the installed folder has the same folder hash as the one we have
     */
    folderHash: string;
}
export interface AddressLibReleaseAssetList {
    version: string;
    // The name of the zip file
    SkyrimSE: Asset;
    // The name of the zip file
    SkyrimAE: Asset;
    // The name of the zip file
    Fallout4: Asset;
}

const AddLibRepoUserName = 'nikitalita';
const AddLibRepoName = 'address-library-dist';

function _getAsset(assetList: AddressLibReleaseAssetList, suffix: AddressLibAssetSuffix): Asset | undefined {
    return assetList[suffix];
}

export interface IAddressLibraryInstallService {
    getInstallState(game: PapyrusGame, modsDir?: string): Promise<AddressLibInstalledState>;
    getDownloadedState(): Promise<AddressLibDownloadedState>;
    DownloadLatestAddressLibs(cancellationToken: CancellationToken): Promise<DownloadResult>;
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

    constructor(
        @inject(ILanguageClientManager) languageClientManager: ILanguageClientManager,
        @inject(IExtensionConfigProvider) configProvider: IExtensionConfigProvider,
        @inject(IPathResolver) pathResolver: IPathResolver
    ) {
        this._pathResolver = pathResolver;
    }

    private static _getAssetZipForSuffixFromRelease(
        release: GithubRelease,
        name: AddressLibAssetSuffix
    ): string | undefined {
        const _assets = release.assets.filter((asset) => asset.name.indexOf(name) >= 0);
        if (_assets.length == 0) {
            return undefined;
        } else if (_assets.length > 1) {
            // This should never happen
            throw new Error('Too many assets found for suffix: ' + name + '');
        }
        return _assets[0].name;
    }

    private static getAssetListFromAddLibRelease(release: GithubRelease): AddressLibReleaseAssetList | undefined {
        let assetZip: string | undefined | Error;
        let ret: AddressLibReleaseAssetList;
        ret.version = release.tag_name;
        for (let _assetSuffix in AddressLibAssetSuffix) {
            const assetSuffix = _assetSuffix as AddressLibAssetSuffix;

            assetZip = AddressLibraryInstallService._getAssetZipForSuffixFromRelease(
                release,
                assetSuffix
            );
            if (!assetZip) {
                return undefined;
            }
            ret[assetSuffix] = {
                zipFile: assetZip,
                folderName: getAddressLibNameFromAssetSuffix(assetSuffix),
                zipFileHash: "0",
                folderHash: "0",
            };
        }
        return ret;
    }

    private static async getLatestAddLibReleaseInfo(): Promise<GithubRelease | undefined> {
        let latestReleaseInfo: GithubRelease | undefined;
        try {
            latestReleaseInfo = await GetLatestReleaseFromRepo(AddLibRepoUserName, AddLibRepoName, false);
            if (!latestReleaseInfo) {
                return undefined;
            }
        } catch (e) {
            return undefined;
        }
        return latestReleaseInfo;
    }

    private static async _downloadLatestAddressLibs(
        downloadFolder: string,
        AssetListDLPath: string,
        cancellationToken: CancellationToken
    ) {
        const latestReleaseInfo = await AddressLibraryInstallService.getLatestAddLibReleaseInfo();
        if (!latestReleaseInfo) {
            return DownloadResult.repoFailure;
        }
        const assetList = AddressLibraryInstallService.getAssetListFromAddLibRelease(latestReleaseInfo);
        if (!assetList) {
            return DownloadResult.repoFailure;
        }
        const release_id = latestReleaseInfo.id;
        // get the shasums
        let sha256SumsPath: string;
        try {
            sha256SumsPath = await downloadAssetFromGitHub(
                AddLibRepoUserName,
                AddLibRepoName,
                release_id,
                'SHA256SUMS.json',
                downloadFolder
            );
            if (!sha256SumsPath) {
                return DownloadResult.sha256sumDownloadFailure;
            }
        } catch (e) {
            return DownloadResult.sha256sumDownloadFailure;
        }

        const sha256buf = fs.readFileSync(sha256SumsPath, 'utf8');
        if (!sha256buf) {
            return DownloadResult.sha256sumDownloadFailure;
        }
        const sha256Sums = JSON.parse(sha256buf);
        const retryLimit = 3;
        let retries = 0;
        for (const _assetSuffix in AddressLibAssetSuffix) {
            const assetSuffix = _assetSuffix as AddressLibAssetSuffix;
            if (cancellationToken.isCancellationRequested) {
                return DownloadResult.cancelled;
            }
            retries = 0;
            const asset = _getAsset(assetList, assetSuffix);
            if (!asset) {
                return DownloadResult.repoFailure;
            }
            const expectedHash = sha256Sums[asset.zipFile];
            if (!expectedHash) {
                return DownloadResult.repoFailure;
            }
            let ret: DownloadResult = await DownloadAssetAndCheckHash(
                AddLibRepoName,
                AddLibRepoUserName,
                release_id,
                asset.zipFile,
                downloadFolder,
                expectedHash
            );

            while (retries < retryLimit && cancellationToken.isCancellationRequested == false) {
                ret = await DownloadAssetAndCheckHash(
                    AddLibRepoName,
                    AddLibRepoUserName,
                    release_id,
                    asset.zipFile,
                    downloadFolder,
                    expectedHash
                );
                if (ret == DownloadResult.success) {
                    break;
                }
                retries++;
            }
            if (cancellationToken.isCancellationRequested) {
                return DownloadResult.cancelled;
            }
            if (ret != DownloadResult.success) {
                return ret;
            }
            asset.zipFileHash = expectedHash;
            const zipFilePath = path.join(downloadFolder, asset.zipFile);
            // We extract the zip here to check the hash of the folder when we check the install state
            // We don't end up installing from the folder, we install from the zip
            const ExtractedFolderPath = path.join(downloadFolder, asset.folderName);
            fs.rmSync(ExtractedFolderPath, { recursive: true, force: true });
            await extractZip(zipFilePath, { dir: ExtractedFolderPath });
            if (!await AddressLibraryInstallService._checkAddlibExtracted(asset.folderName, ExtractedFolderPath)) {
                return DownloadResult.filesystemFailure;
            }
            asset.folderHash = await GetHashOfFolder(ExtractedFolderPath);
            if (asset.folderHash === undefined) {
                return DownloadResult.filesystemFailure;
            }
            // Remove it, because we don't install from it
            fs.rmSync(ExtractedFolderPath, { recursive: true, force: true });
            assetList[assetSuffix] = asset;
        }
        // we do this last to make sure we don't write a corrupt json file
        fs.writeFileSync(AssetListDLPath, JSON.stringify(assetList));
        return DownloadResult.success;
    }

    async DownloadLatestAddressLibs(cancellationToken = new CancellationTokenSource().token): Promise<DownloadResult> {
        const addressLibDownloadPath = await this._pathResolver.getAddressLibraryDownloadFolder();
        const addressLibDLJSONPath = await this._pathResolver.getAddressLibraryDownloadJSON();
        let status = await AddressLibraryInstallService._downloadLatestAddressLibs(
            addressLibDownloadPath,
            addressLibDLJSONPath,
            cancellationToken
        );
        return status;
    }

    private static async _getAssetList(jsonPath: string) {
        if (!fs.existsSync(jsonPath)) {
            return undefined;
        }
        const contents = fs.readFileSync(jsonPath, 'utf8');
        if (!contents || contents.length == 0) {
            // json is corrupt
            return undefined;
        }
        const assetList = JSON.parse(contents) as AddressLibReleaseAssetList;
        if (!assetList) {
            // json is corrupt
            return undefined;
        }
        // check integrity
        for (const _assetSuffix in AddressLibAssetSuffix) {
            const assetSuffix = _assetSuffix as AddressLibAssetSuffix;
            const currentAsset = _getAsset(assetList, assetSuffix);
            if (!currentAsset) {
                // json is corrupt
                return undefined;
            }
            if (
                !currentAsset.zipFile ||
                !currentAsset.zipFileHash ||
                !currentAsset.folderName ||
                !currentAsset.folderHash
            ) {
                // json is corrupt
                return undefined;
            }
        }
        return assetList;
    }

    private async getCurrentDownloadedAssetList(): Promise<AddressLibReleaseAssetList | undefined> {
        const addressLibDLJSONPath = await this._pathResolver.getAddressLibraryDownloadJSON();
        return AddressLibraryInstallService._getAssetList(addressLibDLJSONPath);
    }

    private static async _checkDownloadIntegrity(
        downloadpath: string,
        assetList: AddressLibReleaseAssetList
    ): Promise<boolean> {
        if (!assetList) {
            return false;
        }
        for (const _assetSuffix in AddressLibAssetSuffix) {
            const assetSuffix = _assetSuffix as AddressLibAssetSuffix;
            const currentAsset = _getAsset(assetList, assetSuffix);
            const assetName = currentAsset.zipFile;
            const assetPath = path.join(downloadpath, assetName);
            if (!fs.existsSync(assetPath)) {
                return false;
            }
            if (!CheckHashFile(assetPath, currentAsset.zipFileHash)) {
                return false;
            }
        }
        return true;
    }

    private async checkDownloadIntegrity(): Promise<boolean> {
        const addressLibDownloadPath = await this._pathResolver.getAddressLibraryDownloadFolder();
        const assetList = await this.getCurrentDownloadedAssetList();
        if (!assetList) {
            return false;
        }
        return await AddressLibraryInstallService._checkDownloadIntegrity(addressLibDownloadPath, assetList);
    }

    /**
     * Gets the state of the address library download
     * - If the address library is not downloaded or the download is corrupt, it will return `notDownloaded`
     * - If the address library is downloaded but the version can't be checked, it will return `downloadedButCantCheck`
     * - If the address library is downloaded but the version is outdated, it will return `outdated`
     * - If the address library is downloaded and the version is up to date, it will return `latest`
     * @returns AddressLibDownloadedState
     */
    async getDownloadedState(): Promise<AddressLibDownloadedState> {
        // If it's not downloaded or the download is corrupt, we return notDownloaded
        if (!(await this.checkDownloadIntegrity())) {
            return AddressLibDownloadedState.notDownloaded;
        }
        // At this point, we know if SOME version is downloaded and is valid, but we don't know if it's the latest
        const assetList = await this.getCurrentDownloadedAssetList();
        if (!assetList) {
            return AddressLibDownloadedState.notDownloaded;
        }
        const latestReleaseInfo = await AddressLibraryInstallService.getLatestAddLibReleaseInfo();
        if (!latestReleaseInfo) {
            return AddressLibDownloadedState.downloadedButCantCheckForUpdates;
        }
        const latestAssetList = AddressLibraryInstallService.getAssetListFromAddLibRelease(latestReleaseInfo);
        if (!latestAssetList) {
            return AddressLibDownloadedState.downloadedButCantCheckForUpdates;
        }
        if (latestAssetList.version != assetList.version) {
            return AddressLibDownloadedState.outdated;
        }
        return AddressLibDownloadedState.latest;
    }

    /**
     * @param game  The game to check for
     * @param modsDir The mods directory to check in
     * @param assetList The downloaded Address Library asset list to check against. 
     *   If not provided, we don't check if it's outdated
     * @returns AddressLibInstalledState
     */
    private static async _checkAddressLibsInstalled(
        game: PapyrusGame,
        modsDir: string,
        assetList?: AddressLibReleaseAssetList
    ): Promise<AddressLibInstalledState> {
        const addressLibFolderNames = getAddressLibNames(game);
        for (let _name in addressLibFolderNames) {
            const name = _name as AddressLibraryName;
            const suffix = getAsssetLibraryDLSuffix(name);
            const addressLibInstallPath = path.join(modsDir, name);
            if (!await AddressLibraryInstallService._checkAddlibExtracted(name, addressLibInstallPath)) {
                return AddressLibInstalledState.notInstalled;
            }
            if (assetList) {
                const asset = _getAsset(assetList, suffix);
                if (!asset) {
                    throw new Error('Asset list is corrupt');
                }
                const folderHash = await GetHashOfFolder(addressLibInstallPath);
                if (!folderHash) {
                    return AddressLibInstalledState.notInstalled;
                }
                if (folderHash != asset.folderHash) {
                    return AddressLibInstalledState.outdated;
                }
            }
        }
        return AddressLibInstalledState.installed;
    }

    async getInstallState(game: PapyrusGame, modsDir?: string): Promise<AddressLibInstalledState> {
        const ModsInstallDir = modsDir || (await this._pathResolver.getModParentPath(game));
        const state = await AddressLibraryInstallService._checkAddressLibsInstalled(game, modsDir);
        if (state === AddressLibInstalledState.notInstalled) {
            return AddressLibInstalledState.notInstalled;
        }

        // At this point, we know if the address libraries are installed or not, but we don't know if they're outdated
        const downloadedState = await this.getDownloadedState();
        // We don't check the installed address lib versions if we don't have the latest version downloaded
        if (downloadedState !== AddressLibDownloadedState.latest) {
          return AddressLibInstalledState.installedButCantCheckForUpdates;
        }
        const assetList = await this.getCurrentDownloadedAssetList();
        if (!assetList) {
            return AddressLibInstalledState.installedButCantCheckForUpdates;
        }
        return await AddressLibraryInstallService._checkAddressLibsInstalled(game, ModsInstallDir, assetList);
    }
    /**
     * This checks to see if the folder has at least one file in it
     * @param name 
     * @param installpath - the full path to the folder to check, including the address library name
     * @returns 
     */
    private static async _checkAddlibExtracted(name: AddressLibraryName, installpath: string): Promise<boolean> {
        if (!fs.existsSync(installpath)) {
            return false;
        }
        // TODO: refactor this
        const SEDIR = name.indexOf('SKSE') >= 0 ? 'SKSE' : 'F4SE';
        const pluginsdir = path.join(installpath, SEDIR, 'Plugins');
        const files = fs.readdirSync(pluginsdir, {withFileTypes: true });
        if (files.length == 0) {
            return false;
        }
        return true;
    }

    async installLibraries(
        game: PapyrusGame,
        forceDownload: boolean = false,
        cancellationToken = new CancellationTokenSource().token,
        modsDir: string | undefined
    ): Promise<boolean> {
        const ParentInstallDir = modsDir || (await this._pathResolver.getModParentPath(game));
        const addressLibDownloadPath = await this._pathResolver.getAddressLibraryDownloadFolder();
        let downloadedState = await this.getDownloadedState();
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
        const addressLibNames = getAddressLibNames(game);
        for (let _name in addressLibNames) {
            const name = _name as AddressLibraryName;
            if (cancellationToken.isCancellationRequested) {
                return false;
            }
            const suffix = getAsssetLibraryDLSuffix(name);
            const asset = _getAsset(assetList, suffix);
            if (!asset) {
                throw new Error('Asset list is corrupt');
            }
            const addressLibInstallPath = path.join(ParentInstallDir, name);
            await mkdirIfNeeded(path.dirname(addressLibInstallPath));
            await extractZip(path.join(addressLibDownloadPath, asset.zipFile), {
                dir: path.dirname(addressLibInstallPath),
            });
            if (!await AddressLibraryInstallService._checkAddlibExtracted(name, addressLibInstallPath)) {
                return false;
            }
        }
        return true;
    }
}

export const IAddressLibraryInstallService: interfaces.ServiceIdentifier<IAddressLibraryInstallService> =
    Symbol('AddressLibraryInstallService');
