import * as assert from 'assert';
import { AsyncSerialQueue } from '../../src/common/AsyncSerialQueue';
import {
    IManagedLanguageClient,
    LanguageClientStatus,
    PapyrusLanguageClientManager,
} from '../../src/server/PapyrusLanguageClientManager';

class FakeClient implements IManagedLanguageClient {
    startCount = 0;
    stopCount = 0;
    startShouldThrow: Error | undefined;

    constructor(public readonly serverPath: string) {}

    async start(): Promise<void> {
        this.startCount++;
        if (this.startShouldThrow) {
            throw this.startShouldThrow;
        }
    }

    async stop(): Promise<void> {
        this.stopCount++;
    }

    dispose(): void {
        // Nothing to dispose for the fake.
    }
}

function createManager(options: {
    resolveServerPath: () => string | undefined;
    onCreateClient?: (client: FakeClient) => void;
}) {
    const queue = new AsyncSerialQueue();
    const clients: FakeClient[] = [];

    const manager = new PapyrusLanguageClientManager(
        options.resolveServerPath,
        (serverPath) => {
            const client = new FakeClient(serverPath);
            options.onCreateClient?.(client);
            clients.push(client);
            return client;
        },
        (work) => queue.enqueue(work)
    );

    return { manager, clients };
}

describe('PapyrusLanguageClientManager', () => {
    it('starts with status stopped', () => {
        const { manager } = createManager({ resolveServerPath: () => '/bin/ls' });
        assert.strictEqual(manager.status, LanguageClientStatus.stopped);
    });

    it('reports serverMissing and never creates a client when the path cannot be resolved', async () => {
        const { manager, clients } = createManager({ resolveServerPath: () => undefined });

        await manager.ensureStarted();

        assert.strictEqual(manager.status, LanguageClientStatus.serverMissing);
        assert.strictEqual(clients.length, 0);
    });

    it('starts the client and transitions to running', async () => {
        const statuses: LanguageClientStatus[] = [];
        const { manager, clients } = createManager({ resolveServerPath: () => '/bin/ls' });
        manager.onDidChangeStatus((status) => statuses.push(status));

        await manager.ensureStarted();

        assert.strictEqual(manager.status, LanguageClientStatus.running);
        assert.strictEqual(clients.length, 1);
        assert.strictEqual(clients[0].startCount, 1);
        assert.deepStrictEqual(statuses, [LanguageClientStatus.starting, LanguageClientStatus.running]);
        assert.strictEqual(manager.client, clients[0]);
    });

    it('does not restart if ensureStarted is called again with the same resolved path', async () => {
        const { manager, clients } = createManager({ resolveServerPath: () => '/bin/ls' });

        await manager.ensureStarted();
        await manager.ensureStarted();

        assert.strictEqual(clients.length, 1);
        assert.strictEqual(clients[0].startCount, 1);
        assert.strictEqual(clients[0].stopCount, 0);
    });

    it('restarts the client when the resolved server path changes', async () => {
        let path = '/bin/ls-a';
        const { manager, clients } = createManager({ resolveServerPath: () => path });

        await manager.ensureStarted();
        path = '/bin/ls-b';
        await manager.ensureStarted();

        assert.strictEqual(clients.length, 2);
        assert.strictEqual(clients[0].stopCount, 1);
        assert.strictEqual(clients[1].startCount, 1);
        assert.strictEqual(manager.status, LanguageClientStatus.running);
        assert.strictEqual(manager.client, clients[1]);
    });

    it('transitions from running to serverMissing and stops the old client if the path disappears', async () => {
        let path: string | undefined = '/bin/ls';
        const { manager, clients } = createManager({ resolveServerPath: () => path });

        await manager.ensureStarted();
        path = undefined;
        await manager.ensureStarted();

        assert.strictEqual(manager.status, LanguageClientStatus.serverMissing);
        assert.strictEqual(clients[0].stopCount, 1);
        assert.strictEqual(manager.client, undefined);
    });

    it('reports error status and fires onError if the client throws during start', async () => {
        const errors: string[] = [];
        const { manager } = createManager({
            resolveServerPath: () => '/bin/ls',
            onCreateClient: (client) => {
                client.startShouldThrow = new Error('spawn failed');
            },
        });
        manager.onError((message) => errors.push(message));

        await manager.ensureStarted();

        assert.strictEqual(manager.status, LanguageClientStatus.error);
        assert.deepStrictEqual(errors, ['spawn failed']);
        assert.strictEqual(manager.client, undefined);
    });

    it('stop() tears the client down and reports status stopped', async () => {
        const { manager, clients } = createManager({ resolveServerPath: () => '/bin/ls' });

        await manager.ensureStarted();
        await manager.stop();

        assert.strictEqual(manager.status, LanguageClientStatus.stopped);
        assert.strictEqual(clients[0].stopCount, 1);
        assert.strictEqual(manager.client, undefined);
    });

    it('serializes overlapping ensureStarted/stop calls without interleaving', async () => {
        const { manager, clients } = createManager({ resolveServerPath: () => '/bin/ls' });

        // Fire without awaiting individually -- the manager's internal
        // AsyncSerialQueue must still process these one at a time.
        const first = manager.ensureStarted();
        const second = manager.stop();
        const third = manager.ensureStarted();

        await Promise.all([first, second, third]);

        // start, stop, start -> two clients created, and the manager should
        // have settled back into "running" since the last requested
        // operation was ensureStarted.
        assert.strictEqual(clients.length, 2);
        assert.strictEqual(manager.status, LanguageClientStatus.running);
    });

    it('does not leave the client running after dispose', async () => {
        const { manager, clients } = createManager({ resolveServerPath: () => '/bin/ls' });

        await manager.ensureStarted();
        manager.dispose();

        // dispose() stops the client best-effort/fire-and-forget; give the
        // microtask queue a turn to let it settle.
        await Promise.resolve();
        await Promise.resolve();

        assert.strictEqual(clients[0].stopCount, 1);
    });
});
