import { getReleases } from '@terascope/fetch-github-release/dist/src/getReleases';
import { GithubRelease, GithubReleaseAsset } from '@terascope/fetch-github-release/dist/src/interfaces';
import { downloadRelease } from '@terascope/fetch-github-release/dist/src/downloadRelease';
import { getLatest } from '@terascope/fetch-github-release/dist/src/getLatest';
import * as crypto from 'crypto';
import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

const readdir = promisify(fs.readdir);

export enum DownloadResult {
    success,
    repoFailure,
    sha256sumDownloadFailure,
    filesystemFailure,
    downloadFailure,
    checksumMismatch,
    releaseHasMultipleMatchingAssets,
    cancelled,
}

export async function CheckHash(data: Buffer, expectedHash: string) {
    const hash = crypto.createHash('sha256');
    hash.update(data);
    const actualHash = hash.digest('hex');
    if (expectedHash !== actualHash) {
        return false;
    }
    return true;
}

export async function GetHashOfFolder(folderPath: string, inputHash?: crypto.Hash): Promise<string | undefined>{
  const hash = inputHash ? inputHash : crypto.createHash('sha256');
  const info = await readdir(folderPath, {withFileTypes: true});
  if (!info || info.length == 0) {
    return undefined;
  }
  for (let item of info) {
    const fullPath = path.join(folderPath, item.name);
    if (item.isFile()) {
        const data = fs.readFileSync(fullPath);
        hash.update(data);
    } else if (item.isDirectory()) {
        // recursively walk sub-folders
        await GetHashOfFolder(fullPath, hash);
    }
  }
  return hash.digest('hex');
}

export async function CheckHashOfFolder(folderPath: string, expectedSHA256: string): Promise<boolean> {
    const hash = await GetHashOfFolder(folderPath);
    if (!hash) {
        return false;
    }
    if (hash !== expectedSHA256){
      return false;
    }
    return true;
}

export async function CheckHashFile(filePath: string, expectedSHA256: string) {
    // get the hash of the file
    const file = fs.openSync(filePath, 'r');
    if (!file) {
        return false;
    }
    const buffer = fs.readFileSync(file);
    if (!buffer) {
        return false;
    }
    const hash = crypto.createHash('sha256');
    hash.update(buffer);
    const actualHash = hash.digest('hex');
    if (expectedSHA256 !== actualHash) {
        return false;
    }
    return true;
}

/**
 * Downloads all assets from a specific release
 * @param githubUserName The name of the user or organization that owns the repo
 * @param repoName The name of the repo
 * @param releaseId The id of the release
 * @param downloadFolder The folder to download the assets to
 * @returns An array of paths to the downloaded assets
 * @throws An error if the repo does not exist or the release does not exist
 * @throws An error if the release has multiple assets with the same name
 * @throws An error if the download fails
*/
export async function downloadAssetsFromGitHub(githubUserName: string, repoName: string, releaseId: number, downloadFolder: string): Promise<string[] | undefined> {
  const paths = await downloadRelease(githubUserName, repoName, downloadFolder, (release) => release.id == releaseId, undefined, true);
  if (!paths || paths.length == 0){
    return undefined;
  }
  return paths;
}

/**
 * Downloads a specific asset from a specific release
 * @param githubUserName The name of the user or organization that owns the repo
 * @param repoName The name of the repo
 * @param release_id The id of the release
 * @param assetFileName The file name of the asset to download
 * @param downloadFolder The folder to download the asset to
 * @returns The path to the downloaded asset
 * @throws An error if the repo does not exist or the release does not exist
 * @throws An error if the release has multiple assets with the same name
 * @throws An error if the download fails
 * @throws An error if the asset does not exist in the release
 */
export async function downloadAssetFromGitHub(githubUserName: string, repoName: string, release_id: number, assetFileName: string, downloadFolder: string): Promise<string| undefined>{
  const paths = await downloadRelease(githubUserName, repoName, downloadFolder, (release) => release.id == release_id, (asset) => asset.name === assetFileName, true);
  return (paths && paths.length > 0) ? paths[0] : undefined;
}

/** 
 * Downloads a specific asset from a specific release and checks the hash
 * @param githubUserName The name of the user or organization that owns the repo
 * @param repoName The name of the repo
 * @param release_id The id of the release
 * @param assetFileName The file name of the asset to download
 * @param downloadFolder The folder to download the asset to
 * @param expectedSha256Sum The expected SHA256 hash of the file
 * @returns status of the download
 */
export async function DownloadAssetAndCheckHash(
    githubUserName: string,
    RepoName: string,
    release_id: number,
    assetFileName: string,
    downloadFolder: string,
    expectedSha256Sum: string
): Promise<DownloadResult> {
    let dlPath: string;
    try {
        dlPath = await downloadAssetFromGitHub(githubUserName, RepoName, release_id, assetFileName, downloadFolder);
    } catch (e) {
        return DownloadResult.downloadFailure;
    }
    if (!dlPath) {
        return DownloadResult.downloadFailure;
    }
    // get the hash of the file
    const file = fs.openSync(dlPath, 'r');
    if (!file) {
        return DownloadResult.downloadFailure;
    }
    // get the SHA256 hash of the file using the 'crypto' module
    const buffer = fs.readFileSync(file);
    if (!buffer || buffer.length == 0) {
        return DownloadResult.downloadFailure;
    }
    // get the hash of the file
    if (!CheckHash(buffer, expectedSha256Sum)) {
        fs.rmSync(dlPath);
        return DownloadResult.checksumMismatch;
    }
    return DownloadResult.success;
}

export async function GetLatestReleaseFromRepo(githubUserName: string, repoName: string, prerelease: boolean = false): Promise<GithubRelease | undefined> {
  // if pre-releases == false, filter out pre-releases
  const releaseFilter = !prerelease ? (release: GithubRelease) => release.prerelease == false : undefined;
  const latestRelease = await getLatest(await getReleases(githubUserName, repoName), releaseFilter);
  if (!latestRelease) {
      return undefined;
  }
  return latestRelease;
}