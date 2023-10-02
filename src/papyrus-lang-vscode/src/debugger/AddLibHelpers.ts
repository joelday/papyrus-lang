import { GithubRelease } from '@terascope/fetch-github-release/dist/src/interfaces';
import { AddressLibAssetSuffix, AddressLibraryName } from '../common/constants';
import { getAddressLibNameFromAssetSuffix, getAddressLibNames, getAsssetLibraryDLSuffix } from '../common/GameHelpers';
import {
    DownloadAssetAndCheckHash,
    downloadAssetFromGitHub,
    DownloadResult,
    GetLatestReleaseFromRepo,
} from '../common/GithubHelpers';
import * as fs from 'fs';
import { promisify } from 'util';
import path from 'path';
import extractZip from 'extract-zip';
import { PapyrusGame } from '../PapyrusGame';
import { CheckHashFile, GetHashOfFolder, mkdirIfNeeded } from '../Utilities';
const exists = promisify(fs.exists);
const readFile = promisify(fs.readFile);
const lstat = promisify(fs.lstat);

export const AddLibRepoUserName = 'nikitalita';
export const AddLibRepoName = 'address-library-dist';

export interface AddressLibReleaseAssetList {
    version: string;
    // The name of the zip file
    SkyrimSE: Asset;
    // The name of the zip file
    SkyrimAE: Asset;
    // The name of the zip file
    Fallout4: Asset;
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

export function _getAsset(assetList: AddressLibReleaseAssetList, suffix: AddressLibAssetSuffix): Asset | undefined {
    return assetList[suffix];
}

export function AddLibHelpers() {}
export function GetAssetZipForSuffixFromRelease(
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

export function getAssetListFromAddLibRelease(release: GithubRelease): AddressLibReleaseAssetList | undefined {
    let assetZip: string | undefined | Error;
    const ret: AddressLibReleaseAssetList = new Object() as AddressLibReleaseAssetList;
    ret.version = release.tag_name;
    for (const idx in AddressLibAssetSuffix) {
        const assetSuffix: AddressLibAssetSuffix = AddressLibAssetSuffix[idx as keyof typeof AddressLibAssetSuffix];
        assetZip = GetAssetZipForSuffixFromRelease(release, assetSuffix);
        if (!assetZip) {
            return undefined;
        }
        ret[assetSuffix] = {
            zipFile: assetZip,
            folderName: getAddressLibNameFromAssetSuffix(assetSuffix),
            zipFileHash: '0',
            folderHash: '0',
        };
    }
    return ret;
}

export async function getLatestAddLibReleaseInfo(): Promise<GithubRelease | undefined> {
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

interface CancellationToken {
    isCancellationRequested: boolean;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onCancellationRequested: any;
}

export async function DownloadLatestAddressLibs(
    downloadFolder: string,
    AssetListDLPath: string,
    cancellationToken: CancellationToken
) {
    const latestReleaseInfo = await getLatestAddLibReleaseInfo();
    if (!latestReleaseInfo) {
        return DownloadResult.repoFailure;
    }
    const assetList = getAssetListFromAddLibRelease(latestReleaseInfo);
    if (!assetList) {
        return DownloadResult.repoFailure;
    }
    const release_id = latestReleaseInfo.id;
    // get the shasums
    let sha256SumsPath: string | undefined;
    try {
        sha256SumsPath = await downloadAssetFromGitHub(
            AddLibRepoUserName,
            AddLibRepoName,
            release_id,
            'SHA256SUMS.json',
            downloadFolder
        );
    } catch (e) {
        return DownloadResult.sha256sumDownloadFailure;
    }
    if (!sha256SumsPath || !(await exists(sha256SumsPath)) || !(await lstat(sha256SumsPath)).isFile()) {
        return DownloadResult.sha256sumDownloadFailure;
    }
    const sha256buf = await readFile(sha256SumsPath, 'utf8');
    if (!sha256buf) {
        return DownloadResult.sha256sumDownloadFailure;
    }
    const sha256Sums = JSON.parse(sha256buf);
    const retryLimit = 3;
    let retries = 0;
    for (const idx in AddressLibAssetSuffix) {
        const assetSuffix: AddressLibAssetSuffix = AddressLibAssetSuffix[idx as keyof typeof AddressLibAssetSuffix];

        if (cancellationToken.isCancellationRequested) return DownloadResult.cancelled;

        retries = 0;
        const asset = _getAsset(assetList, assetSuffix);
        if (!asset) {
            return DownloadResult.repoFailure;
        }
        const expectedHash = sha256Sums[asset.zipFile];
        if (!expectedHash) {
            return DownloadResult.repoFailure;
        }

        let ret: DownloadResult = DownloadResult.downloadFailure;
        while (retries < retryLimit && cancellationToken.isCancellationRequested == false) {
            ret = await DownloadAssetAndCheckHash(
                AddLibRepoUserName,
                AddLibRepoName,
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

        if (cancellationToken.isCancellationRequested) return DownloadResult.cancelled;

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
        if (cancellationToken.isCancellationRequested) return DownloadResult.cancelled;
        if (!(await _checkAddlibExtracted(asset.folderName, downloadFolder))) {
            return DownloadResult.filesystemFailure;
        }
        const folderHash = await GetHashOfFolder(ExtractedFolderPath);
        if (!folderHash) {
            return DownloadResult.filesystemFailure;
        }
        asset.folderHash = folderHash;
        if (cancellationToken.isCancellationRequested) return DownloadResult.cancelled;
        // Remove it, because we don't install from it
        fs.rmSync(ExtractedFolderPath, { recursive: true, force: true });
        assetList[assetSuffix] = asset;
    }
    // we do this last to make sure we don't write a corrupt json file
    fs.writeFileSync(AssetListDLPath, JSON.stringify(assetList));
    return DownloadResult.success;
}

export async function GetAssetList(jsonPath: string) {
    if (!fs.existsSync(jsonPath) || !fs.lstatSync(jsonPath).isFile()) {
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
    for (const idx in AddressLibAssetSuffix) {
        const assetSuffix: AddressLibAssetSuffix = AddressLibAssetSuffix[idx as keyof typeof AddressLibAssetSuffix];
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

export async function _checkDownloadIntegrity(
    downloadpath: string,
    assetList: AddressLibReleaseAssetList
): Promise<boolean> {
    if (!assetList) {
        return false;
    }
    for (const idx in AddressLibAssetSuffix) {
        const assetSuffix: AddressLibAssetSuffix = AddressLibAssetSuffix[idx as keyof typeof AddressLibAssetSuffix];
        const currentAsset = _getAsset(assetList, assetSuffix);
        if (!currentAsset) {
            return false;
        }
        const assetName = currentAsset.zipFile;
        const assetPath = path.join(downloadpath, assetName);
        if (!(await exists(assetPath))) {
            return false;
        }
        if (!CheckHashFile(assetPath, currentAsset.zipFileHash)) {
            return false;
        }
    }
    return true;
}

/**
 * This checks to see if the ${modsDir}/${name}/{SK,F4}SE/Plugins folder has at least one file in it
 * @param name - the name of the address library
 * @param modsDir - the mods directory to check in
 * @returns true or false
 */
export async function _checkAddlibExtracted(name: AddressLibraryName, modsDir: string): Promise<boolean> {
    const addressLibInstallPath = path.join(modsDir, name);
    // TODO: refactor this
    const SEDIR = name.indexOf('SKSE') >= 0 ? 'SKSE' : 'F4SE';
    const pluginsdir = path.join(addressLibInstallPath, SEDIR, 'Plugins');

    if (!fs.existsSync(pluginsdir) || !fs.lstatSync(pluginsdir).isDirectory()) {
        return false;
    }
    const files = fs.readdirSync(pluginsdir, { withFileTypes: true });
    if (files.length == 0) {
        return false;
    }
    return true;
}
enum AddressLibInstalledState {
    notInstalled,
    installed,
    outdated,
    installedButCantCheckForUpdates,
}
/**
 * Gets the state of the address library install
 *
 * @param name The name of the address library
 * @param modsDir The mods directory to check in
 * @param assetList The downloaded Address Library asset list to check against.
 *   If not provided, we don't check if it's outdated
 * @returns AddressLibInstalledState
 */
export async function _checkAddressLibInstalled(
    name: AddressLibraryName,
    modsDir: string,
    assetList?: AddressLibReleaseAssetList
): Promise<AddressLibInstalledState> {
    if (!(await _checkAddlibExtracted(name, modsDir))) {
        return AddressLibInstalledState.notInstalled;
    }
    if (assetList) {
        const addressLibInstallPath = path.join(modsDir, name);
        const suffix = getAsssetLibraryDLSuffix(name);
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
    return AddressLibInstalledState.installed;
}

/**
 * @param game  The game to check for
 * @param modsDir The mods directory to check in
 * @param assetList The downloaded Address Library asset list to check against.
 *   If not provided, we don't check if it's outdated
 * @returns AddressLibInstalledState
 */
export async function _checkAddressLibsInstalled(
    game: PapyrusGame,
    modsDir: string,
    assetList?: AddressLibReleaseAssetList
): Promise<AddressLibInstalledState> {
    const addressLibFolderNames = getAddressLibNames(game);
    for (const name of addressLibFolderNames) {
        const state = await _checkAddressLibInstalled(name, modsDir, assetList);
        if (state !== AddressLibInstalledState.installed) {
            return state;
        }
    }
    return AddressLibInstalledState.installed;
}

// TODO: For some godforsaken reason, the address library names on Nexus mods for both SE and AE are the same.
// (i.e. "Address Library for SKSE Plugins")
// Need to handle this
export async function _installAddressLibs(
    game: PapyrusGame,
    ParentInstallDir: string,
    downloadDir: string,
    assetList: AddressLibReleaseAssetList,
    cancellationToken: CancellationToken
): Promise<boolean> {
    const addressLibNames = getAddressLibNames(game);
    for (const name of addressLibNames) {
        if (cancellationToken.isCancellationRequested) {
            return false;
        }
        const addressLibInstallPath = path.join(ParentInstallDir, name);
        // The reason we check each individiual library is that Skyrim currently requires two libraries,
        // So we want to make sure we don't overwrite one that is already installed
        // We don't currently check if the library is outdated or not
        const state = await _checkAddressLibInstalled(name, ParentInstallDir);
        if (state === AddressLibInstalledState.installed) {
            // It's installed and we're not forcing updates, so we don't need to do anything
            continue;
        }
        const suffix = getAsssetLibraryDLSuffix(name);
        const asset = _getAsset(assetList, suffix);
        if (!asset) {
            throw new Error('Asset list is corrupt');
        }
        const zipPath = path.join(downloadDir, asset.zipFile);
        // fs.rmSync(addressLibInstallPath, { recursive: true, force: true });
        await mkdirIfNeeded(addressLibInstallPath);
        await extractZip(zipPath, {
            dir: addressLibInstallPath,
        });
        if (!(await _checkAddressLibInstalled(name, ParentInstallDir))) {
            return false;
        }
    }
    return true;
}
