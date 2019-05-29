import {
    ProviderResult,
    DebugAdapterDescriptorFactory,
    DebugSession,
    DebugAdapterExecutable,
    DebugAdapterDescriptor,
    debug,
    Disposable,
    DebugConfiguration,
    ExtensionContext,
} from 'vscode';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { take } from 'rxjs/operators';
import { IPapyrusDebugSession } from './PapyrusDebugSession';
import { toCommandLineArgs } from '../Utilities';
import { IExtensionContext } from '../common/vscode/IocDecorators';

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
    private readonly _registration: Disposable;

    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._configProvider = configProvider;
        this._context = context;

        this._registration = debug.registerDebugAdapterDescriptorFactory('papyrus', this);
    }

    async createDebugAdapterDescriptor(
        session: IPapyrusDebugSession,
        executable: DebugAdapterExecutable
    ): Promise<DebugAdapterDescriptor> {
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
