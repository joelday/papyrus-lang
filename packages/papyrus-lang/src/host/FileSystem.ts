import { createDecorator } from 'decoration-ioc';
export interface IFileSystem {
    readTextFile(uri: string): string;
    findFilesAsUris(globPattern: string): string[];
}

// tslint:disable-next-line:variable-name
export const IFileSystem = createDecorator<IFileSystem>('fileSystem');
