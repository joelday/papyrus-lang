import {
    Descriptor,
    InstantiationService,
    ServiceCollection,
} from 'decoration-ioc';
import * as path from 'path';
import URI from 'vscode-uri';
import { StringBuilder } from '../../src/common/StringBuilder';
import { IFileSystem } from '../../src/host/FileSystem';
import { NodeFileSystem } from '../../src/host/NodeFileSystem';
import { Program } from '../../src/program/Program';
import {
    IXmlProjectConfigParser,
    XmlProjectConfigParser,
} from '../../src/projects/XmlProjectConfigParser';
import { XmlProjectLoader } from '../../src/projects/XmlProjectLoader';
import { FileSystemScriptTextProvider } from '../../src/sources/FileSystemScriptTextProvider';
import { IScriptTextProvider } from '../../src/sources/ScriptTextProvider';

describe('Program', () => {
    const serviceCollection = new ServiceCollection(
        [IFileSystem, new Descriptor(NodeFileSystem)],
        [IXmlProjectConfigParser, new Descriptor(XmlProjectConfigParser)],
        [IScriptTextProvider, new Descriptor(FileSystemScriptTextProvider)]
    );

    const instantiationService = new InstantiationService(serviceCollection);
    const projectLoader = instantiationService.createInstance(XmlProjectLoader);

    const projectConfig = projectLoader.loadProject(
        URI.file(
            path.resolve(
                __dirname,
                '../../../../papyrus/FO4TestScripts/Project/Project.ppj'
            )
        ).toString()
    );

    it('Properly enumerates project files and parses scripts on demand', () => {
        const program = instantiationService.createInstance(
            Program,
            projectConfig
        ) as Program;

        const scriptFile = program.getScriptFileByName('Functions');

        expect(scriptFile).toBeTruthy();
        expect(scriptFile.scriptNode.scriptNode).toBeTruthy();
    });

    it(
        'Performs static analysis of scripts',
        () => {
            const program = instantiationService.createInstance(
                Program,
                projectConfig
            );

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
