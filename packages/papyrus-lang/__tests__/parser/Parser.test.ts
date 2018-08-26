import { Parser } from '../../src/parser/Parser';
import { Tokenizer } from '../../src/tokenizer/Tokenizer';

import * as path from 'path';
import { findFiles, readTextFile } from '../../src/common/Utilities';
import { Diagnostics } from '../../src/Diagnostics';

describe('Parser', () => {
    const tokenizer = new Tokenizer();
    const parser = new Parser();

    it('parses base scripts without errors', () => {
        const files = findFiles(
            path.resolve(__dirname, '../../../../papyrus/FO4Scripts/Base/**/*.psc')
        );

        for (const file of files) {
            const source = readTextFile(file);
            const diagnostics = new Diagnostics(file, source);
            const tokens = tokenizer.tokenize(source, diagnostics);
            parser.parse(tokens, diagnostics);

            for (const error of diagnostics.errors) {
                console.error(error.toString());
            }

            expect(diagnostics.errors.length).toBe(0);
        }
    });
});
