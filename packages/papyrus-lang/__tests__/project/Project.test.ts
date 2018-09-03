import * as path from 'path';
import URI from 'vscode-uri';
import { NodeFileSystem } from '../../src/host/NodeFileSystem';
import { loadProjectFile, Project } from '../../src/program/Project';
import { parsePapyrusProjectXml } from '../../src/program/ProjectConfig';

describe('Project', () => {
    const fileSystem = new NodeFileSystem();

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

            const project = parsePapyrusProjectXml(source);
            expect(project).toMatchSnapshot();
        });
    });

    describe('loadProjectFile', () => {
        it('parses a ppj file and creates a config with resolved paths', () => {
            loadProjectFile(
                path.resolve(
                    __dirname,
                    '../../../../papyrus/FO4TestScripts/Project/Project.ppj'
                )
            );
        });
    });

    // it('resolves files with the correct precedence', () => {
    //     const projectConfig = loadProjectFile(
    //         path.resolve(__dirname, '../../../../papyrus/FO4TestScripts/Project/Project.ppj')
    //     );
    //     const project = new Project(projectConfig);
    //     const files = Array.from(project.resolveFiles().entries());

    //     expect(files[0][1]).not.toBe(files[1][1]);
    // });
});
