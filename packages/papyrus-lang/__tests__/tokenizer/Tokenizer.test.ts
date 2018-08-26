import * as path from 'path';
import { findFiles, readTextFile } from '../../src/common/Utilities';
import { Diagnostics } from '../../src/Diagnostics';
import { Tokenizer } from '../../src/tokenizer/Tokenizer';

describe('Tokenizer', () => {
    const tokenizer = new Tokenizer();

    // TODO: Assert on results.
    // TODO: Formalize all source file based tests and add convenience features.
    it('returns a correctly parsed array of tokens', () => {
        const source = readTextFile(
            path.resolve(
                __dirname,
                '../../../../papyrus/FO4Scripts/Base/AbCourserSpeedScript.psc'
            )
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
        const source = readTextFile(
            path.resolve(__dirname, 'scripts/unterminatedString.psc')
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
        const source = readTextFile(
            path.resolve(
                __dirname,
                '../../../../papyrus/FO4Scripts/Base/DefaultRemoteControlObjectReference.psc'
            )
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
            const files = findFiles(
                path.resolve(
                    __dirname,
                    '../../../../papyrus/FO4Scripts/Base/**/*.psc'
                )
            );

            for (const file of files) {
                const source = readTextFile(file);
                const diagnostics = new Diagnostics(file, source);
                Array.from(tokenizer.tokenize(source, diagnostics));
                expect(diagnostics.errors).toMatchObject([]);
            }
        },
        60000
    );
});
