import { asScannableIterator, createIterator } from '../src/common/Utilities';

describe('Utilities', () => {
    describe('asScannableIterator', () => {
        it('Creates an iterator that can scan forward and backward.', () => {
            const iterator = createIterator([0, 1, 2, 3, 4, 5]);
            const scannable = asScannableIterator(iterator);

            expect(scannable.current).toBeNull();

            expect(scannable.next()).toMatchObject({ value: 0, done: false });

            expect(scannable.previous()).toBeNull();
            expect(scannable.previous()).toBeNull();

            expect(scannable.next()).toMatchObject({ value: 0, done: false });
            expect(scannable.current).toMatchObject({ value: 0, done: false });

            expect(scannable.next()).toMatchObject({ value: 1, done: false });
            expect(scannable.next()).toMatchObject({ value: 2, done: false });

            expect(scannable.previous()).toMatchObject({
                value: 1,
                done: false,
            });

            expect(scannable.current).toMatchObject({ value: 1, done: false });
            expect(scannable.next()).toMatchObject({ value: 2, done: false });
            expect(scannable.peek()).toMatchObject({ value: 3, done: false });
            expect(scannable.current).toMatchObject({ value: 2, done: false });

            expect(scannable.next()).toMatchObject({ value: 3, done: false });
            expect(scannable.next()).toMatchObject({ value: 4, done: false });
            expect(scannable.next()).toMatchObject({ value: 5, done: false });

            expect(scannable.next()).toMatchObject({
                value: undefined,
                done: true,
            });
            expect(scannable.next()).toMatchObject({
                value: undefined,
                done: true,
            });
        });

        it('Produces a functioning iterable', () => {
            const values = [0, 1, 2, 3, 4, 5];
            const iterator = createIterator(values);
            const scannable = asScannableIterator(iterator);

            expect(Array.from(scannable)).toMatchObject(values);
        });
    });
});
