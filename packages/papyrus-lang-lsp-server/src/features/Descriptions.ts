import { StringBuilder } from 'papyrus-lang/lib/common/StringBuilder';
import { DisplayTextEmitter } from 'papyrus-lang/lib/program/DisplayTextEmitter';
import { Symbol, SymbolKind } from 'papyrus-lang/lib/symbols/Symbol';

export function buildHoverText(
    symbol: Symbol,
    displayTextEmitter: DisplayTextEmitter
) {
    if (symbol.kind === SymbolKind.Intrinsic) {
        return null;
    }

    const displayText = displayTextEmitter.getDisplayText(symbol);

    const sb = new StringBuilder();
    sb.appendLine('```papyrus');
    sb.appendLine(`(${displayText.kind})`);
    sb.appendLine(displayText.text);
    sb.appendLine('```');

    if (displayText.documentation) {
        sb.appendLine(displayText.documentation);
    }

    return sb.toString();
}
