import { Range } from './common/Range';
import { StringBuilder } from './common/StringBuilder';
import { stringOf } from './common/Utilities';

// Arbitrary change.

export class DiagnosticsError {
    private readonly _diagnostics: Diagnostics;
    private readonly _message: string;
    private readonly _range: Range;

    public get fileName() {
        return this._diagnostics.fileName;
    }

    public get source() {
        return this._diagnostics.source;
    }

    public get message() {
        return this._message;
    }

    public get range() {
        return this._range;
    }

    protected get diagnostics() {
        return this._diagnostics;
    }

    constructor(diagnostics: Diagnostics, message: string, range: Range) {
        this._diagnostics = diagnostics;
        this._message = message;
        this._range = range;
    }

    public toString(maxLength: number = 120) {
        const length = this.range.end - this.range.start;
        const availablePadding = Math.max(
            0,
            Math.floor((maxLength - length - 3) / 2)
        );

        const startPosition = Math.max(0, this.range.start - availablePadding);
        const startOffset = this.range.start - startPosition;
        const endPosition = Math.min(
            this._diagnostics.source.length,
            this.range.end + availablePadding
        );

        const sourceText = this._diagnostics.source.substr(
            startPosition,
            endPosition - startPosition
        );

        const cleanedSourceText = sourceText.replace(/\r|\n|\t/g, ' ');

        const sb = new StringBuilder();
        sb.appendLine(
            `Error at ${this.range.start},${this.range.end} in ${
                this._diagnostics.fileName
            }:`
        );

        sb.append('   ');
        sb.append(cleanedSourceText);
        sb.appendLine();

        sb.append('   ');
        sb.append(stringOf(' ', startOffset));

        sb.append('^');
        sb.append(stringOf('~', length - 1));
        sb.append(' ');
        sb.append(this._message);

        return sb.toString();
    }
}

export class Diagnostics {
    private readonly _fileName: string;
    private readonly _source: string;
    private _parent: Diagnostics;
    private _errors: DiagnosticsError[] = [];

    get fileName(): string {
        return this._fileName || this._parent.fileName;
    }

    get source(): string {
        return this._source || this._parent.source;
    }

    get errors(): ReadonlyArray<DiagnosticsError> {
        return this._errors;
    }

    constructor(fileName: string, source: string) {
        this._fileName = fileName;
        this._source = source;
    }

    public addError(message: string, range: Range) {
        const error = new DiagnosticsError(this, message, range);
        this._errors.push(error);
        return error;
    }
}
