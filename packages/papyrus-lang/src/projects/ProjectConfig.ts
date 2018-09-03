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

export function createEmptyConfig(): ProjectConfig {
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
