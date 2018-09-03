export interface ProjectSource {
    findProjectFiles(rootUri: string);
    loadProjectFile(uri: string);
}

// export function findProjectFiles(dirPath: string) {
//     return findFiles(path.normalize(path.join(dirPath, '**', '*.ppj')));
// }

// export function findProjectFilesInDirectories(dirPaths: string[]) {
//     return Array.from(
//         new Set(iterateMany<string>(dirPaths.map(findProjectFiles))).values()
//     );
// }

// export function loadProjectFile(filePath: string) {
//     const resolvedPath = path.resolve(filePath);
//     const base = path.dirname(resolvedPath);
//     const projectXml = readTextFile(filePath);

//     const projectConfig = parsePapyrusProjectXml(projectXml);
//     projectConfig.folder.path = path.resolve(
//         base,
//         path.normalizeSafe(projectConfig.folder.path)
//     );

//     projectConfig.imports = projectConfig.imports
//         .map((importPath) => path.normalizeSafe(importPath))
//         .map(
//             (importPath) =>
//                 path.isAbsolute(importPath)
//                     ? importPath
//                     : path.resolve(base, importPath)
//         );

//     projectConfig.filePath = filePath;

//     if (projectConfig.output) {
//         projectConfig.output = path.normalizeSafe(projectConfig.output);
//     }

//     return projectConfig;
// }
