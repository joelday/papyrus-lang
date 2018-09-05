import * as path from 'path';
import URI from 'vscode-uri';
import { NodeFileSystem } from '../../src/host/NodeFileSystem';
import { XmlProjectConfigParser } from '../../src/projects/XmlProjectConfigParser';
import { XmlProjectLoader } from '../../src/projects/XmlProjectLoader';

describe('Projects', () => {
    const fileSystem = new NodeFileSystem();
    const projectConfigParser = new XmlProjectConfigParser();
    const projectLoader = new XmlProjectLoader(fileSystem, projectConfigParser);

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

    describe('loadProject', () => {
        it('parses a ppj file and creates a config with resolved paths', () => {
            projectLoader.loadProject(
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
