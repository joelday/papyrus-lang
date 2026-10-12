import * as assert from 'assert';
import { AsyncSerialQueue } from '../../src/common/AsyncSerialQueue';

describe('AsyncSerialQueue', () => {
    it('runs enqueued work in FIFO order without interleaving', async () => {
        const queue = new AsyncSerialQueue();
        const order: number[] = [];

        const first = queue.enqueue(async () => {
            await delay(20);
            order.push(1);
            return 1;
        });

        const second = queue.enqueue(async () => {
            order.push(2);
            return 2;
        });

        assert.strictEqual(await first, 1);
        assert.strictEqual(await second, 2);
        assert.deepStrictEqual(order, [1, 2]);
    });

    it('still runs later work after an earlier operation throws', async () => {
        const queue = new AsyncSerialQueue();

        const failing = queue.enqueue(async () => {
            throw new Error('boom');
        });

        const succeeding = queue.enqueue(async () => 'ok');

        await assert.rejects(failing, /boom/);
        assert.strictEqual(await succeeding, 'ok');
    });

    it('propagates each operation its own result independently', async () => {
        const queue = new AsyncSerialQueue();

        const results = await Promise.all([
            queue.enqueue(async () => 'a'),
            queue.enqueue(async () => 'b'),
            queue.enqueue(async () => 'c'),
        ]);

        assert.deepStrictEqual(results, ['a', 'b', 'c']);
    });
});

function delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}
