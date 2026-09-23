import * as assert from 'assert';
import * as path from 'path';
import { getRuntimeIdentifier, ServerPathResolver } from '../../src/server/ServerPathResolver';

describe('getRuntimeIdentifier', () => {
    it('maps win32/x64 to win-x64', () => {
        assert.strictEqual(getRuntimeIdentifier('win32', 'x64'), 'win-x64');
    });

    it('maps linux/x64 to linux-x64', () => {
        assert.strictEqual(getRuntimeIdentifier('linux', 'x64'), 'linux-x64');
    });

    it('maps darwin/arm64 to osx-arm64', () => {
        assert.strictEqual(getRuntimeIdentifier('darwin', 'arm64'), 'osx-arm64');
    });

    it('maps darwin/x64 to osx-x64', () => {
        assert.strictEqual(getRuntimeIdentifier('darwin', 'x64'), 'osx-x64');
    });

    it('returns undefined for unsupported combinations (e.g. win32/arm64)', () => {
        assert.strictEqual(getRuntimeIdentifier('win32', 'arm64'), undefined);
        assert.strictEqual(getRuntimeIdentifier('linux', 'arm64'), undefined);
        assert.strictEqual(getRuntimeIdentifier('freebsd', 'x64'), undefined);
    });
});

describe('ServerPathResolver', () => {
    it('prefers an override path if the file exists', () => {
        const resolver = new ServerPathResolver(
            '/ext',
            () => '/custom/Papyrus.LanguageServer',
            (candidate) => candidate === '/custom/Papyrus.LanguageServer',
            'darwin',
            'arm64'
        );

        assert.strictEqual(resolver.resolve(), '/custom/Papyrus.LanguageServer');
    });

    it('returns undefined if the override path does not exist, without falling back to the bundled path', () => {
        const resolver = new ServerPathResolver(
            '/ext',
            () => '/custom/missing',
            () => false,
            'darwin',
            'arm64'
        );

        assert.strictEqual(resolver.resolve(), undefined);
    });

    it('resolves the bundled per-platform binary when there is no override', () => {
        const expected = path.join('/ext', 'bin', 'osx-arm64', 'Papyrus.LanguageServer');

        const resolver = new ServerPathResolver(
            '/ext',
            () => undefined,
            (candidate) => candidate === expected,
            'darwin',
            'arm64'
        );

        assert.strictEqual(resolver.resolve(), expected);
    });

    it('appends .exe on win32', () => {
        const expected = path.join('/ext', 'bin', 'win-x64', 'Papyrus.LanguageServer.exe');

        const resolver = new ServerPathResolver(
            '/ext',
            () => undefined,
            (candidate) => candidate === expected,
            'win32',
            'x64'
        );

        assert.strictEqual(resolver.resolve(), expected);
    });

    it('returns undefined when the bundled binary is missing', () => {
        const resolver = new ServerPathResolver(
            '/ext',
            () => undefined,
            () => false,
            'darwin',
            'arm64'
        );

        assert.strictEqual(resolver.resolve(), undefined);
    });

    it('returns undefined for an unsupported platform/arch combination', () => {
        const resolver = new ServerPathResolver(
            '/ext',
            () => undefined,
            () => true,
            'win32',
            'arm64'
        );

        assert.strictEqual(resolver.resolve(), undefined);
    });
});
