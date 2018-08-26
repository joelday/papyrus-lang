import { xml2js } from 'xml-js';

export enum AssemblyMode {
    none = 'none',
    keep = 'keep',
    only = 'only',
    discard = 'discard',
}

export interface ProjectConfig {
    filePath?: string;
    output?: string;
    flags?: string;
    asm?: AssemblyMode;
    optimize?: boolean;
    release?: boolean;
    final?: boolean;
    imports: string[];
    folder: { noRecurse?: boolean; path: string };
    scripts?: string[];
}

function getEmptyProject(): ProjectConfig {
    return {
        output: null,
        flags: null,
        asm: 'none' as AssemblyMode,
        optimize: false,
        release: false,
        final: false,
        imports: null,
        folder: { noRecurse: false, path: '.' },
        scripts: null,
    };
}

interface XmlProjectAttributes {
    Flags: string;
    Output: string;
    Asm: AssemblyMode;
    Optimize: string;
    Release: string;
    Final: string;
}

interface XmlFolderAttributes {
    NoRecurse: string;
}

interface XmlImport {
    _text: string[];
}

interface XmlImports {
    Import: XmlImport[];
}

interface XmlFolder {
    _attributes: XmlFolderAttributes;
    _text: string[];
}

interface XmlFolders {
    Folder: XmlFolder[];
}

interface XmlScript {
    _text: string[];
}

interface XmlScripts {
    Script: XmlScript[];
}

interface XmlPapyrusProject {
    _attributes: XmlProjectAttributes;
    Imports: XmlImports[];
    Folders: XmlFolders[];
    Scripts: XmlScripts[];
}

interface XmlRootObject {
    PapyrusProject: XmlPapyrusProject[];
}

function parseBool(bool: string) {
    return JSON.parse(bool.toString().toLowerCase());
}

export function parsePapyrusProjectXml(xml: string): ProjectConfig {
    const parsed = xml2js(xml, {
        alwaysArray: true,
        compact: true,
        ignoreAttributes: false,
    }) as XmlRootObject;

    const project: ProjectConfig = { ...getEmptyProject() };

    const xmlProject = parsed.PapyrusProject[0];
    const projectAttributes = xmlProject._attributes;
    const imports = xmlProject.Imports ? xmlProject.Imports[0] : null;

    project.imports =
        imports && imports.Import ? imports.Import.map((i) => i._text[0]) : [];

    const folders = xmlProject.Folders ? xmlProject.Folders[0] : null;
    project.folder =
        folders && folders.Folder && folders.Folder.length > 0
            ? {
                  noRecurse:
                      folders.Folder[0]._attributes &&
                      folders.Folder[0]._attributes.NoRecurse
                          ? parseBool(folders.Folder[0]._attributes.NoRecurse)
                          : false,
                  path: folders.Folder[0]._text[0],
              }
            : null;

    const scripts = xmlProject.Scripts ? xmlProject.Scripts[0] : null;
    project.scripts =
        scripts && scripts.Script ? scripts.Script.map((s) => s._text[0]) : [];

    if (projectAttributes) {
        if (projectAttributes.Flags) {
            project.flags = projectAttributes.Flags;
        }

        if (projectAttributes.Asm) {
            project.asm = projectAttributes.Asm;
        }

        if (projectAttributes.Final) {
            project.final = parseBool(projectAttributes.Final);
        }

        if (projectAttributes.Optimize) {
            project.optimize = parseBool(projectAttributes.Optimize);
        }

        if (projectAttributes.Output) {
            project.output = projectAttributes.Output;
        }

        if (projectAttributes.Release) {
            project.release = parseBool(projectAttributes.Release);
        }
    }

    return project;
}
