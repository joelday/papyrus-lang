import { Diagnostics } from '../Diagnostics';
import { ScriptNode } from '../parser/Node';
import { SymbolDeclarationVisitor } from './SymbolDeclarationVisitor';

export class SymbolBinder {
    public bindSymbols(scriptNode: ScriptNode, diagnostics: Diagnostics) {
        const definitionVisitor = new SymbolDeclarationVisitor(diagnostics);
        definitionVisitor.visitScript(scriptNode);
    }
}
