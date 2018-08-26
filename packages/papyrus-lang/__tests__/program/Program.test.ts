import * as path from 'path';
import { StringBuilder } from '../../src/common/StringBuilder';
import { Diagnostics } from '../../src/Diagnostics';
import { Program } from '../../src/program/Program';
import { loadProjectFile, Project } from '../../src/program/Project';

describe('Program', () => {
    it('Properly enumerates project files and parses scripts on demand', () => {
        const projectConfig = loadProjectFile(
            path.resolve(
                __dirname,
                '../../../../papyrus/FO4TestScripts/Project/Project.ppj'
            )
        );

        const program = new Program(projectConfig);
        const scriptFile = program.getScriptFileByName('Functions');

        expect(scriptFile).toBeTruthy();
        expect(scriptFile.scriptNode.scriptNode).toBeTruthy();
    });

    it(
        'Performs static analysis of scripts',
        () => {
            const projectConfig = loadProjectFile(
                path.resolve(
                    __dirname,
                    '../../../../papyrus/FO4TestScripts/Project/Project.ppj'
                )
            );

            const program = new Program(projectConfig);
            let errorCount = 0;

            const diagnosticsOutput = new StringBuilder();

            for (const scriptName of program.scriptNames.slice(0, 100)) {
                const scriptFile = program.getScriptFileByName(scriptName);
                if (!scriptFile) {
                    continue;
                }

                const diagnostics = scriptFile.validateTypesAndReferences();

                if (diagnostics.errors.length > 0) {
                    diagnosticsOutput.appendLine(`${scriptName}:`);

                    for (const error of diagnostics.errors) {
                        errorCount++;

                        diagnosticsOutput.appendLine(error.toString());
                        diagnosticsOutput.appendLine();
                    }

                    diagnosticsOutput.appendLine();
                }
            }

            if (errorCount > 0) {
                console.log(diagnosticsOutput.toString());
            }

            expect(errorCount).toBe(0);
        },
        60000
    );
});
