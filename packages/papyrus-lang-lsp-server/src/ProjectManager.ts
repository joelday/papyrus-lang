import { IInstantiationService } from 'decoration-ioc';
import { iterateMany } from 'papyrus-lang/lib/common/Utilities';
import { IProjectSource } from 'papyrus-lang/lib/projects/ProjectSource';
import { ProjectHost } from './ProjectHost';

export class ProjectManager {
    private readonly _projectSource: IProjectSource;
    private readonly _instantiationService: IInstantiationService;
    private readonly _hosts: Map<string, ProjectHost> = new Map();

    constructor(
        @IProjectSource projectSource: IProjectSource,
        @IInstantiationService instantiationService: IInstantiationService
    ) {
        this._projectSource = projectSource;
        this._instantiationService = instantiationService;
    }

    public updateProjects(workspaceDirs: string[], reloadProjects: boolean) {
        const projectUris = this._projectSource.findProjectFiles(workspaceDirs);

        for (const projectUri of projectUris) {
            if (!this._hosts.has(projectUri)) {
                console.log(`Loading Papyrus project: ${projectUri}`);

                this._hosts.set(
                    projectUri,
                    this._instantiationService.createInstance(
                        ProjectHost,
                        projectUri
                    )
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

        for (const hostUri of this._hosts.keys()) {
            if (!projectUris.includes(hostUri)) {
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

        // TODO: Support for files outside of projects.

        return null;
    }

    get projectHosts() {
        return Array.from(this._hosts.values());
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
}
