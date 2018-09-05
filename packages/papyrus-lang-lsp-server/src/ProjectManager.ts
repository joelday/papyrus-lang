import { IInstantiationService } from 'decoration-ioc';
import { iterateMany } from 'papyrus-lang/lib/common/Utilities';
import { AmbientProjectLoader } from 'papyrus-lang/lib/projects/AmbientProjectLoader';
import { IProjectLoader } from 'papyrus-lang/lib/projects/ProjectLoader';
import { IXmlProjectLoader } from 'papyrus-lang/lib/projects/XmlProjectLoader';
import { IXmlProjectLocator } from 'papyrus-lang/lib/projects/XmlProjectLocator';
import { ProjectHost } from './ProjectHost';

export class ProjectManager {
    get projectHosts() {
        return Array.from(this._hosts.values());
    }
    private readonly _projectLocator: IXmlProjectLocator;
    private readonly _xmlProjectLoader: IProjectLoader;
    private readonly _instantiationService: IInstantiationService;
    private readonly _hosts: Map<string, ProjectHost> = new Map();

    constructor(
        @IXmlProjectLoader xmlProjectLoader: IProjectLoader,
        @IXmlProjectLocator projectLocator: IXmlProjectLocator,
        @IInstantiationService instantiationService: IInstantiationService
    ) {
        this._xmlProjectLoader = xmlProjectLoader;
        this._projectLocator = projectLocator;
        this._instantiationService = instantiationService;
    }

    public updateProjects(workspaceDirs: string[], reloadProjects: boolean) {
        const allProjectUris: string[] = [];

        for (const workspaceDir of workspaceDirs) {
            const projectUris = this._projectLocator.findProjectFiles(
                workspaceDir
            );

            if (projectUris.length === 0) {
                allProjectUris.push(workspaceDir);
                this.createOrUpdateHost(
                    workspaceDir,
                    new AmbientProjectLoader([]),
                    reloadProjects
                );
            } else {
                allProjectUris.push(...projectUris);
                for (const projectUri of projectUris) {
                    this.createOrUpdateHost(
                        projectUri,
                        this._xmlProjectLoader,
                        reloadProjects
                    );
                }
            }
        }

        for (const hostUri of this._hosts.keys()) {
            if (!allProjectUris.includes(hostUri)) {
                this._hosts.delete(hostUri);
            }
        }
    }

    public getScriptFileByUri(uri: string) {
        // Get the first explicit instance.
        for (const host of this._hosts.values()) {
            const file = host.program.getScriptFileByUri(uri, true);
            if (file) {
                return file;
            }
        }

        // If there are any hosts, just get an anonymous instance from the first one.
        if (this._hosts.size > 0) {
            return this._hosts
                .values()
                .next()
                .value.program.getScriptFileByUri(uri);
        }

        return null;
    }

    public getAllScriptNames() {
        return Array.from(
            new Set(
                iterateMany<string>(
                    this.projectHosts.map((h) => h.program.scriptNames)
                )
            ).values()
        );
    }

    private createOrUpdateHost(
        projectUri: string,
        loader: IProjectLoader,
        reloadProjects: boolean
    ) {
        if (!this._hosts.has(projectUri)) {
            console.log(`Loading Papyrus project: ${projectUri}`);

            this._hosts.set(
                projectUri,
                new ProjectHost(projectUri, loader, this._instantiationService)
            );
        } else {
            const host = this._hosts.get(projectUri);

            if (reloadProjects) {
                host.reloadProject();
            } else {
                host.refreshProjectFiles();
            }
        }
    }
}
