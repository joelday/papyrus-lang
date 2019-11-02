import { Disposable } from 'vscode-jsonrpc';
import { workspace } from 'vscode';
import { createDecorator } from 'decoration-ioc';
import { IExtensionConfigProvider, ExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { ExtensionContext, CancellationToken, Progress, CancellationTokenSource } from 'vscode';
import { Observable, Subscriber } from 'rxjs';
import { take } from 'rxjs/operators';
import { resolveInstallPath } from '../Utilities';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);
const removeFile = promisify(fs.unlink);

export enum WorkspaceSetupServiceState {
    idle,
    start,
    notSetup,                       // no .vscode setup at all
    notPapyrus,                     // not supposed to be a papyrus project
    isPapyrus,                      // is an already setup Papyrus project
    skyrimNotSetup,                 // looks like a Skyrim Classic (Oldrim) data dir but not setup
    skyrimSpecialEditionNotSetup,   // looks like a Skyrim Special Edition data dir but not setup
    fallout4NotSetup,               // looks like a Fallout 4 data dir but not setup
    skyrimSetup,                    // Is Skyrim and Is Setup now
    skyrimSpecialEditionSetup,      // Is Skyrim Special Edition and is setup now
    fallout4Setup,                  // Is Fallout4 and Is Setup now
    cancelled,
    error,
    done
}

export interface IWorkspaceSetupService {
    run(): Promise<void>;
    getState(): Promise<WorkspaceSetupServiceState>;
}

export class WorkspaceSetupService implements IWorkspaceSetupService {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;
    private _state: WorkspaceSetupServiceState = WorkspaceSetupServiceState.idle;

    constructor(
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._configProvider = configProvider;
        this._context = context;

        workspace.onDidChangeWorkspaceFolders(event => {
            console.log(event);
            this.run();
        });

        workspace.onDidOpenTextDocument(event => {
            console.log(event);
            this.run();
        });

    }


    async run(): Promise<void> {
        if (this._state !== WorkspaceSetupServiceState.idle) {
            return;
        } else {
            console.log("Workspace setup service started.");
            setImmediate(() => { this._setState(WorkspaceSetupServiceState.start); });
            return;
        }
    }

    async getState(): Promise<WorkspaceSetupServiceState> {
        return WorkspaceSetupServiceState.notPapyrus;
    }

    async _setState(currentState: WorkspaceSetupServiceState): Promise<void> {
        const oldState = this._state;
        this._state = currentState;
        var newState: WorkspaceSetupServiceState = WorkspaceSetupServiceState.idle; // default go to idle

        console.log(`WorkspaceSetupService state changed from ${WorkspaceSetupServiceState[oldState]} to ${WorkspaceSetupServiceState[currentState]}`);

        switch (currentState) {

            case WorkspaceSetupServiceState.idle:
                // Back to idle. Just return so we don't spin from idle to idle
                return;

            case WorkspaceSetupServiceState.start:
                // Does .vscode or *.code-workspace exist?
                newState = WorkspaceSetupServiceState.notSetup;
                break;

            case WorkspaceSetupServiceState.notSetup:
                // if not, IS this workspace is a candidate for setting up as a papyrus project of some sort?
                // Identify directory based on: flags file name and location
                // Get game directory from registry. Does this match game directory?
                // go to next state based on what we find
                break;

            case WorkspaceSetupServiceState.notPapyrus:
                // No, does not appear to be a Papyrus-related workspace
                newState = WorkspaceSetupServiceState.done;
                break;

            case WorkspaceSetupServiceState.isPapyrus:
                // Seems to be a Papyrus workspace but can't tell what game!
                // Ask what game?
                // switch state based on what we find
                newState = WorkspaceSetupServiceState.done;
                break;

            case WorkspaceSetupServiceState.skyrimNotSetup:
                // Appears to be a classic Skyrim data folder but isn't setup
                newState = WorkspaceSetupServiceState.skyrimSetup;
                break;

            case WorkspaceSetupServiceState.skyrimSpecialEditionNotSetup:
                // Appears to be a Skyrim Special Edition data folder but isn't setup
                newState = WorkspaceSetupServiceState.skyrimSpecialEditionSetup;
                break;

            case WorkspaceSetupServiceState.fallout4NotSetup:
                // Appears to be a Fallout 4 data folder but isn't setup
                newState = WorkspaceSetupServiceState.fallout4Setup;
                break;

            case WorkspaceSetupServiceState.skyrimSetup:
                // Skyrim Classic data directory and has now just been setup
                newState = WorkspaceSetupServiceState.done;
                break;

            case WorkspaceSetupServiceState.skyrimSpecialEditionSetup:
                // Skyrim Special Edition data directory and has just now been setup
                newState = WorkspaceSetupServiceState.done;
                break;

            case WorkspaceSetupServiceState.fallout4Setup:
                // Fallout4 data directory and has just now been setup
                newState = WorkspaceSetupServiceState.done;
                break;


            case WorkspaceSetupServiceState.error:
                console.log("WorkspaceSerup error!");
                newState = WorkspaceSetupServiceState.idle;
                break;

            case WorkspaceSetupServiceState.cancelled:
                // Cancelled by user
                console.log("WorkspaceSetup cancelled by user");
                newState = WorkspaceSetupServiceState.idle;
                break;

            case WorkspaceSetupServiceState.done:
                // Everything is done now. back to idle in case we are needed again
                console.log("WorkspaceSetupService: done!");
                newState = WorkspaceSetupServiceState.idle;
                break;

        }

        // This is done on the event loop so we aren't recursing and building up stack frames and can run async
        setImmediate(() => { this._setState(newState); });
        return;
    }
}

export const IWorkspaceSetupService = createDecorator<IWorkspaceSetupService>('workspaceSetupService');