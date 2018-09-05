import { IAmbientImportsProvider } from './AmbientImportsProvider';
import { createEmptyConfig, ProjectConfig } from './ProjectConfig';
import { IProjectLoader } from './ProjectLoader';

export class AmbientProjectLoader implements IProjectLoader {
    private readonly _ambientImportsProvider: IAmbientImportsProvider;

    constructor(
        @IAmbientImportsProvider ambientImportsProvider: IAmbientImportsProvider
    ) {
        this._ambientImportsProvider = ambientImportsProvider;
    }

    public loadProject(uri: string): ProjectConfig {
        return {
            ...createEmptyConfig(),
            imports: [
                ...this._ambientImportsProvider.getAmbientImportsForUri(uri),
            ],
            folder: { path: uri },
        };
    }
}
