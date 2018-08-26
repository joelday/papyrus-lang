import * as path from 'path';

import { ExtensionContext, workspace } from 'vscode';

import { watch } from 'chokidar';
import { FSWatcher } from 'fs';
import { Deferred } from 'papyrus-lang/lib/common/Deferred';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind,
} from 'vscode-languageclient';
import { ClientServer } from './ClientServer';

let clientServer: ClientServer;
let watcher: FSWatcher;

function delayAsync(durationMs: number) {
    const deferred = new Deferred<void>();
    setTimeout(() => deferred.resolve(), durationMs);
    return deferred.promise;
}

function getModuleEntryPointPath(moduleName: string) {
    return path.join(getModuleSourcePath(moduleName), 'index.js');
}

function getModuleSourcePath(moduleName: string) {
    return path.join('node_modules', moduleName, 'lib');
}

export function activate(context: ExtensionContext) {
    const serverModulePath = context.asAbsolutePath(
        getModuleEntryPointPath('papyrus-lang-lsp-server')
    );

    clientServer = new ClientServer(serverModulePath);

    const serverLibrarySourcePath = context.asAbsolutePath(
        getModuleSourcePath('papyrus-lang-lsp-server')
    );

    const languageLibrarySourcePath = context.asAbsolutePath(
        getModuleSourcePath('papyrus-lang')
    );

    clientServer.start();

    let hasRefreshed: boolean = false;
    let isRestarting: boolean = false;
    watcher = watch([serverLibrarySourcePath, languageLibrarySourcePath]);
    watcher.on('all', async () => {
        if (hasRefreshed) {
            hasRefreshed = true;
            return;
        }

        if (isRestarting) {
            return;
        }

        try {
            isRestarting = true;
            await delayAsync(3000);
            await clientServer.restart();
            await delayAsync(3000);
        } finally {
            isRestarting = false;
        }
    });
}

export async function deactivate() {
    if (!clientServer) {
        return;
    }

    await clientServer.stop();
}
