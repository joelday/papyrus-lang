import { RequestType } from 'vscode-languageclient';

export interface ProjectInfosParams {}

export interface ProjectInfoScript {
    identifier: string;
    filePath: string;
}

export interface ProjectInfoSourceInclude {
    name: string;
    fullPath: string;
    scripts: ProjectInfoScript[];
}

export interface ProjectInfo {
    name: string;
    sourceIncludes: ProjectInfoSourceInclude[];
}

export interface ProjectInfos {
    projects: ProjectInfo[];
}

export const projectInfosRequestType = {
    type: new RequestType<ProjectInfosParams, ProjectInfos | null, void, void>('papyrus/projectInfos'),
};
