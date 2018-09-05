import { createEmptyConfig, ProjectConfig } from './ProjectConfig';
import { IProjectLoader } from './ProjectLoader';

export class AmbientProjectLoader implements IProjectLoader {
    private readonly _importUris: string[];

    constructor(importUris: string[] = []) {
        this._importUris = importUris;
    }

    public loadProject(uri: string): ProjectConfig {
        return {
            ...createEmptyConfig(),
            imports: [...this._importUris],
            folder: { path: uri },
        };
    }
}
