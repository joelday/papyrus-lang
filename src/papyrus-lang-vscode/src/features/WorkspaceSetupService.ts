import { Disposable } from 'vscode-jsonrpc';
import { } from 'vscode';
import { createDecorator } from 'decoration-ioc';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { ExtensionContext, CancellationToken, Progress, CancellationTokenSource } from 'vscode';
import { take } from 'rxjs/operators';
import { resolveInstallPath } from '../Utilities';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);
const removeFile = promisify(fs.unlink);

export enum WorkspaceSetupServiceState {
    notSetup,                       // no .vscode setup at all
    notPapyrus,                     // not a papyrus project
    skyrimNotSetup,                 // looks like a Skyrim Classic (Oldrim) data dir but not setup
    skyrimSpecialEditionNotSetup,   // looks like a Skyrim Special Edition data dir but not setup
    fallout4NotSetup,               // looks like a Fallout 4 data dir but not setup
    skyrimSetup,                    // Is Skyrim and Is Setup now
    skyrimSpecialEditionSetup,      // Is Skyrim Special Edition and is setup now
    fallout4Setup                   // Is Fallout4 and Is Setup now
}

export interface IWorkspaceSetupService {
    getSetupState(): Promise<WorkspaceSetupServiceState>;
}

// Does .vscode or *.code-workspace exist?
// Identify directory based on: flags file name and location
// Get game directory from registry. Does this match game directory?
// 

export class WorkspaceSetupService implements IWorkspaceSetupService {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;

    constructor(
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._configProvider = configProvider;
        this._context = context;
    }

    async getSetupState(): Promise<WorkspaceSetupServiceState> {
        return WorkspaceSetupServiceState.notPapyrus;
    }
}

export const IWorkspaceSetupService = createDecorator<IWorkspaceSetupService>('workspaceSetupService');