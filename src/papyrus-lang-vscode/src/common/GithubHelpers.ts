import { getReleases } from '@terascope/fetch-github-release/dist/src/getReleases';
import { GithubRelease, GithubReleaseAsset } from '@terascope/fetch-github-release/dist/src/interfaces';
import { downloadRelease } from '@terascope/fetch-github-release/dist/src/downloadRelease';
import { getLatest } from '@terascope/fetch-github-release/dist/src/getLatest';
import * as fs from 'fs';
import { promisify } from 'util';
import { CheckHashFile } from '../Utilities';

const readdir = promisify(fs.readdir);
const exists = promisify(fs.exists);
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
    let dlPath: string | undefined;
    try {
        dlPath = await downloadAssetFromGitHub(githubUserName, RepoName, release_id, assetFileName, downloadFolder);
    } catch (e) {
        return DownloadResult.downloadFailure;
    }
    if (!dlPath || !await exists(dlPath)) {
        return DownloadResult.downloadFailure;
    }
    if (!CheckHashFile(dlPath, expectedSha256Sum)) {
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