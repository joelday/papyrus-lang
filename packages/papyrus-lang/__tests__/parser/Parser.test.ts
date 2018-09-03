import * as path from 'path';
import { Diagnostics } from '../../src/Diagnostics';
import { NodeFileSystem } from '../../src/host/NodeFileSystem';
import { Parser } from '../../src/parser/Parser';
import { Tokenizer } from '../../src/tokenizer/Tokenizer';

describe('Parser', () => {
    const tokenizer = new Tokenizer();
    const parser = new Parser();

    it('parses base scripts without errors', () => {
        const fileSystem = new NodeFileSystem();

        const files = fileSystem.findFilesAsUris(
            path.resolve(
                __dirname,
                '../../../../papyrus/FO4Scripts/Base/**/*.psc'
            )
        );

        for (const file of files) {
            const source = fileSystem.readTextFile(file);
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
