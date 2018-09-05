import { createDecorator } from 'decoration-ioc';
import { ProjectConfig } from './ProjectConfig';

export interface IProjectLoader {
    loadProject(uri: string): ProjectConfig;
}

// tslint:disable-next-line:variable-name
export const IProjectLoader = createDecorator<IProjectLoader>('projectLoader');
