import { EOL } from 'os';

export enum LineEnding {
    Lf = '\n',
    CrLf = '\r\n',
}

export class StringBuilder {
    private readonly _newLine: string;
    private _string: string = '';

    constructor(newLine: LineEnding = EOL as LineEnding) {
        this._newLine = newLine;
    }

    public append(str: string) {
        this._string += str;
    }

    public appendLine(str?: string) {
        if (str) {
            this.append(str);
        }

        this.append(this._newLine);
    }

    public toString() {
        return this._string;
    }
}
