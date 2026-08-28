import * as path from 'path';

/**
 * Maps Node's `process.platform`/`process.arch` to the .NET RID naming used
 * by the CI publish matrix (see `.github/workflows/managed.yml`), e.g.
 * `artifacts/Papyrus.LanguageServer-win-x64/`.
 */
export function getRuntimeIdentifier(platform: NodeJS.Platform, arch: string): string | undefined {
    const archName = arch === 'arm64' ? 'arm64' : arch === 'x64' ? 'x64' : undefined;
    if (!archName) {
        return undefined;
    }

    switch (platform) {
        case 'win32':
            return archName === 'x64' ? 'win-x64' : undefined;
        case 'linux':
            return archName === 'x64' ? 'linux-x64' : undefined;
        case 'darwin':
            return `osx-${archName}`;
        default:
            return undefined;
    }
}

function getExecutableName(platform: NodeJS.Platform): string {
    return platform === 'win32' ? 'Papyrus.LanguageServer.exe' : 'Papyrus.LanguageServer';
}

export interface IServerPathResolver {
    /**
     * Resolves the path to the `Papyrus.LanguageServer` executable that
     * should be launched for this workspace, or `undefined` if none could be
     * found (e.g. an unsupported platform/architecture, or a missing bundled
     * binary). Callers should surface `undefined` as a clear "unsupported
     * platform" error rather than silently failing to start.
     */
    resolve(): string | undefined;
}

/**
 * Resolves the bundled, platform-specific `Papyrus.LanguageServer` AOT
 * executable, or an explicit user-configured override path.
 *
 * Unlike the old extension's `PathResolver`/`CreationKitInfoProvider`, there
 * is no ambient Creation Kit install-path detection or per-game compiler
 * resolution here: the new language server is a single self-contained
 * executable that discovers `.ppj` project files itself via the LSP
 * `initialize` workspace folders, so this resolver's only job is finding
 * *that one binary*.
 */
export class ServerPathResolver implements IServerPathResolver {
    constructor(
        private readonly _extensionRoot: string,
        private readonly _getOverridePath: () => string | undefined,
        private readonly _fileExists: (candidatePath: string) => boolean,
        private readonly _platform: NodeJS.Platform = process.platform,
        private readonly _arch: string = process.arch
    ) {}

    resolve(): string | undefined {
        const overridePath = this._getOverridePath();
        if (overridePath) {
            return this._fileExists(overridePath) ? overridePath : undefined;
        }

        const rid = getRuntimeIdentifier(this._platform, this._arch);
        if (!rid) {
            return undefined;
        }

        const bundledPath = path.join(this._extensionRoot, 'bin', rid, getExecutableName(this._platform));
        return this._fileExists(bundledPath) ? bundledPath : undefined;
    }
}
