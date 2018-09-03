import * as path from 'path';
import URI from 'vscode-uri';
import { NodeFileSystem } from '../../src/host/NodeFileSystem';
import { ProjectConfigParser } from '../../src/projects/ProjectConfigParser';
import { ProjectSource } from '../../src/projects/ProjectSource';

describe('Projects', () => {
    const fileSystem = new NodeFileSystem();
    const projectConfigParser = new ProjectConfigParser();
    const projectSource = new ProjectSource(fileSystem, projectConfigParser);

    describe('parsePapyrusProjectXml', () => {
        it('parses a ppj file', () => {
            const source = fileSystem.readTextFile(
                URI.file(
                    path.resolve(
                        __dirname,
                        '../../../../papyrus/FO4TestScripts/Project/Project.ppj'
                    )
                ).toString()
            );

            const project = projectConfigParser.parseConfig(source);
            expect(project).toMatchSnapshot();
        });
    });

    describe('loadProjectFile', () => {
        it('parses a ppj file and creates a config with resolved paths', () => {
            projectSource.loadProjectFile(
                URI.file(
                    path.resolve(
                        __dirname,
                        '../../../../papyrus/FO4TestScripts/Project/Project.ppj'
                    )
                ).toString()
            );
        });
    });
});
