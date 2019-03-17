import { OperatorFunction, using, concat, NEVER, from, of } from 'rxjs';
import { FileSystemWatcher, workspace } from 'vscode';
import { map, switchMap, mergeMap, distinctUntilChanged } from 'rxjs/operators';
import * as fs from 'fs';
import { promisify } from 'util';
import { UnsubscribableDisposableProxy } from '../../Reactive';
import { eventToValueObservable } from './Events';

const readFile = promisify(fs.readFile);

export function file(): OperatorFunction<string, Buffer> {
    return (ob) =>
        ob.pipe(
            distinctUntilChanged(),
            switchMap((filePath: string) =>
                using<Buffer>(
                    () => new UnsubscribableDisposableProxy<FileSystemWatcher>(),
                    (proxy: UnsubscribableDisposableProxy<FileSystemWatcher>) => {
                        const watcher = workspace.createFileSystemWatcher(filePath, true);
                        proxy.disposable = watcher;

                        return eventToValueObservable(
                            watcher.onDidChange,
                            () => readFile(filePath),
                            (uri) => readFile(uri.fsPath)
                        ).pipe(mergeMap((p) => from(p)));
                    }
                )
            ),
            distinctUntilChanged((x, y) => x.compare(y) === 0)
        );
}

export function textFile(encoding = 'utf8'): OperatorFunction<string, string> {
    return (ob) =>
        ob.pipe(
            file(),
            map((b) => b.toString(encoding))
        );
}
