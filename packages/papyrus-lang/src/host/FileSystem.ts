import { createDecorator } from 'decoration-ioc';
export interface FileSystem {
    readTextFile(uri: string): string;
    findFilesAsUris(globPattern: string): string[];
}

// tslint:disable-next-line:variable-name
export const FileSystem = createDecorator<FileSystem>('fileSystem');
