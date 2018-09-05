import { createDecorator } from 'decoration-ioc';

export interface IAmbientImportsProvider {
    getAmbientImportsForUri(uri: string): string[];
}

// tslint:disable-next-line:variable-name
export const IAmbientImportsProvider = createDecorator<IAmbientImportsProvider>(
    'ambientImportsProvider'
);
