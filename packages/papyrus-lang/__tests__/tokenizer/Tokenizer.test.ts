import * as path from 'path';
import URI from 'vscode-uri';
import { Diagnostics } from '../../src/Diagnostics';
import { NodeFileSystem } from '../../src/host/NodeFileSystem';
import { Tokenizer } from '../../src/tokenizer/Tokenizer';

describe('Tokenizer', () => {
    const fileSystem = new NodeFileSystem();
    const tokenizer = new Tokenizer();

    // TODO: Assert on results.
    // TODO: Formalize all source file based tests and add convenience features.
    it('returns a correctly parsed array of tokens', () => {
        const source = fileSystem.readTextFile(
            URI.file(
                path.resolve(
                    __dirname,
                    '../../../../papyrus/FO4Scripts/Base/AbCourserSpeedScript.psc'
                )
            ).toString()
        );

        const tokens = Array.from(
            tokenizer.tokenize(
                source,
                new Diagnostics('AbCourserSpeedScript.psc', source)
            )
        ).map((t) => t.toString());

        expect(tokens).toMatchSnapshot();
    });

    it('notifies with an error', () => {
        const source = fileSystem.readTextFile(
            URI.file(
                path.resolve(__dirname, 'scripts/unterminatedString.psc')
            ).toString()
        );

        const diagnostics = new Diagnostics('unterminatedString.psc', source);
        // Tokenizing twice to ensure onError is checked before calling.
        const tokens = Array.from(tokenizer.tokenize(source, diagnostics)).map(
            (t) => t.toString()
        );
        expect(tokens).toMatchSnapshot();
        tokenizer.tokenize(
            source,
            new Diagnostics('unterminatedString.psc', source)
        );
        expect(diagnostics.errors.length).toBeGreaterThan(0);
        expect(diagnostics.errors.map((e) => e.toString())).toMatchSnapshot();
    });

    it('returns tokens that make up the entire text of the source file', () => {
        const source = fileSystem.readTextFile(
            URI.file(
                path.resolve(
                    __dirname,
                    '../../../../papyrus/FO4Scripts/Base/DefaultRemoteControlObjectReference.psc'
                )
            ).toString()
        );

        const tokens = Array.from(
            tokenizer.tokenize(
                source,
                new Diagnostics(
                    'DefaultRemoteControlObjectReference.psc',
                    source
                )
            )
        );
        expect(tokens.map((t) => t.text).join('')).toBe(source);
    });

    it(
        'tokenizes base scripts without errors',
        () => {
            const files = fileSystem.findFilesAsUris(
                path.resolve(
                    __dirname,
                    '../../../../papyrus/FO4Scripts/Base/**/*.psc'
                )
            );

            for (const file of files) {
                const source = fileSystem.readTextFile(file);
                const diagnostics = new Diagnostics(file, source);
                Array.from(tokenizer.tokenize(source, diagnostics));
                expect(diagnostics.errors).toMatchObject([]);
            }
        },
        60000
    );
});
