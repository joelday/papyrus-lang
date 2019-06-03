import {
    DebugAdapterDescriptorFactory,
    DebugAdapterExecutable,
    DebugAdapterDescriptor,
    debug,
    Disposable,
    ExtensionContext,
    window,
    commands,
} from 'vscode';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { take } from 'rxjs/operators';
import { IPapyrusDebugSession } from './PapyrusDebugSession';
import { toCommandLineArgs } from '../Utilities';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { IDebugSupportInstaller, DebugSupportInstallState, DebugSupportInstaller } from './DebugSupportInstaller';

export interface IDebugToolArguments {
    port?: number;
    projectPath?: string;
    defaultScriptSourceFolder?: string;
    defaultAdditionalImports?: string;
    creationKitInstallPath: string;
    relativeIniPaths: string[];
}

export class PapyrusDebugAdapterDescriptorFactory implements DebugAdapterDescriptorFactory {
    private readonly _creationKitInfoProvider: ICreationKitInfoProvider;
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;
    private readonly _debugSupportInstaller: IDebugSupportInstaller;
    private readonly _registration: Disposable;

    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext,
        @IDebugSupportInstaller debugSupportInstaller: IDebugSupportInstaller
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._configProvider = configProvider;
        this._context = context;
        this._debugSupportInstaller = debugSupportInstaller;

        this._registration = debug.registerDebugAdapterDescriptorFactory('papyrus', this);
    }

    async createDebugAdapterDescriptor(
        session: IPapyrusDebugSession,
        executable: DebugAdapterExecutable
    ): Promise<DebugAdapterDescriptor> {
        // TODO: See if there's a better place to do pre-start checks:

        const noopExecutable = new DebugAdapterExecutable('');
        const installState = await this._debugSupportInstaller.getInstallState();

        switch (installState) {
            case DebugSupportInstallState.incorrectVersion:
                if (
                    (await window.showWarningMessage(
                        'The current language support plugin is out of date. Without updating, the debug session may not be reliable. Make sure Fallout 4 is closed before updating.',
                        'Update',
                        'Ignore'
                    )) === 'Update'
                ) {
                    commands.executeCommand('papyrus.fallout4.installDebuggerSupport');
                    return noopExecutable;
                }
                break;
            case DebugSupportInstallState.missing:
                if (
                    (await window.showInformationMessage(
                        'Papyrus debugging requires an F4SE plugin to be installed. After installing, relaunch Fallout 4 with F4SE.',
                        'Install',
                        'Cancel'
                    )) === 'Install'
                ) {
                    commands.executeCommand('papyrus.fallout4.installDebuggerSupport');
                }

                return noopExecutable;
            case DebugSupportInstallState.gameDisabled:
                window.showErrorMessage(
                    'Fallout 4 language support must be enabled before installing the Papyrus debugger plugin.'
                );
                return noopExecutable;
            case DebugSupportInstallState.gameMissing:
                window.showErrorMessage('Unable to locate Fallout 4 install path.');
                return noopExecutable;
            case DebugSupportInstallState.cancelled:
                return noopExecutable;
        }

        const config = (await this._configProvider.config.pipe(take(1)).toPromise()).fallout4;
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(PapyrusGame.fallout4)
            .pipe(take(1))
            .toPromise();

        const toolArguments: IDebugToolArguments = {
            port: session.configuration.port || 2077,
            projectPath: session.configuration.projectPath,
            creationKitInstallPath: creationKitInfo.resolvedInstallPath,
            relativeIniPaths: config.creationKitIniFiles,
            defaultScriptSourceFolder: creationKitInfo.config.Papyrus.sScriptSourceFolder,
            defaultAdditionalImports: creationKitInfo.config.Papyrus.sAdditionalImports,
        };

        const newExecutable = new DebugAdapterExecutable(
            this._context.asAbsolutePath(getDebugToolPath()),
            toCommandLineArgs(toolArguments)
        );

        return newExecutable;
    }

    dispose() {
        this._registration.dispose();
    }
}

function getDebugToolPath() {
    return './debug-bin/Debug/net461/DarkId.Papyrus.DebugAdapterProxy/DarkId.Papyrus.DebugAdapterProxy.exe';
}
