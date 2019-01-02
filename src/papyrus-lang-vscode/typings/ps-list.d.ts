declare module 'ps-list' {
    interface Process {
        readonly pid: number;
        readonly name: string;
        readonly cmd?: string;
        readonly ppid: number;
        readonly cpu?: number;
        readonly memory?: number;
    }

    export default function psList(): Promise<Process[]>;
}
