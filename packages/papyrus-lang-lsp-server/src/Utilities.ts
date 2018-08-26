import { Range as PapyrusRange } from 'papyrus-lang/lib/common/Range';
import { Range, TextDocument } from 'vscode-languageserver';

export function papyrusRangeToRange(
    textDocument: TextDocument,
    range: PapyrusRange
): Range {
    return {
        start: textDocument.positionAt(range.start),
        end: textDocument.positionAt(range.end),
    };
}

export function getTextForRange(text: string, range: PapyrusRange) {
    return text.substr(range.start, range.end - range.start);
}
