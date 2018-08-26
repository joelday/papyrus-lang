import { StringBuilder } from '../common/StringBuilder';
import { Flags, isLiteral, NodeKind } from '../parser/Node';
import {
    CustomEventSymbol,
    EventSymbol,
    FunctionSymbol,
    GroupSymbol,
    ImportSymbol,
    ParameterSymbol,
    PropertySymbol,
    ScriptSymbol,
    StateSymbol,
    Symbol,
    SymbolKind,
    VariableSymbol,
} from '../symbols/Symbol';
import { Program } from './Program';

export interface DisplayText {
    kind: string;
    text: string;
    documentation?: string;
}

export interface EventDisplayText {
    kind: string;
    prefix: string;
    parameters: DisplayText[];
    documentation?: string;
}

export interface FunctionDisplayText {
    kind: string;
    prefix: string;
    shortNamePrefix: string;
    parameters: DisplayText[];
    postfix: string;
    documentation?: string;
}

export class DisplayTextEmitter {
    private readonly _program: Program;

    constructor(program: Program) {
        this._program = program;
    }

    public getDisplayText(symbol: Symbol): DisplayText {
        switch (symbol.kind) {
            case SymbolKind.Function:
                const funcDisplayText = this.getDisplayTextForFunction(symbol);
                return {
                    kind: funcDisplayText.kind,
                    text: `${
                        funcDisplayText.prefix
                    }(${funcDisplayText.parameters
                        .map((p) => p.text)
                        .join(', ')})${funcDisplayText.postfix}`,
                    documentation: funcDisplayText.documentation,
                };
            case SymbolKind.Event:
                const eventDisplayText = this.getDisplayTextForEvent(symbol);
                return {
                    kind: eventDisplayText.kind,
                    text: `${
                        eventDisplayText.prefix
                    }(${eventDisplayText.parameters
                        .map((p) => p.text)
                        .join(', ')})`,
                    documentation: eventDisplayText.documentation,
                };
            case SymbolKind.Script:
                return this.getDisplayTextForScript(symbol);
            case SymbolKind.State:
                return this.getDisplayTextForState(symbol);
            case SymbolKind.Variable:
                return this.getDisplayTextForVariable(symbol);
            case SymbolKind.Property:
                return this.getDisplayTextForProperty(symbol);
            case SymbolKind.Parameter:
                return this.getDisplayTextForParameter(symbol);
            case SymbolKind.CustomEvent:
                return this.getBasicDisplayText(
                    symbol.fullyQualifiedName,
                    'custom event',
                    'CustomEvent'
                );
            case SymbolKind.Group:
                return this.getBasicDisplayText(
                    symbol.fullyQualifiedName,
                    'group',
                    'Group'
                );
            case SymbolKind.Import:
                return this.getBasicDisplayText(
                    symbol.name,
                    'import',
                    'Import'
                );
            case SymbolKind.Struct:
                return this.getBasicDisplayText(
                    symbol.fullyQualifiedName,
                    'struct',
                    'Struct'
                );
            default:
                return null;
        }
    }

    public getDisplayTextForEvent(symbol: EventSymbol): EventDisplayText {
        const prefix = new StringBuilder();

        prefix.append(`Event ${symbol.fullyQualifiedName}`);

        return {
            kind: 'event',
            prefix: prefix.toString(),
            parameters: symbol.parameters.map((p) =>
                this.getDisplayTextForParameter(p)
            ),
            documentation: symbol.documentation,
        };
    }

    public getDisplayTextForFunction(
        symbol: FunctionSymbol
    ): FunctionDisplayText {
        const prefix = new StringBuilder();
        const shortNamePrefix = new StringBuilder();

        if (symbol.returnType) {
            const returnType = this._program.getTypeForName(
                symbol.returnType.name
            );
            prefix.append(
                `${
                    returnType
                        ? returnType.fullyQualifiedName
                        : symbol.returnType.name
                } `
            );
        }

        shortNamePrefix.append(prefix.toString());

        prefix.append(`Function ${symbol.fullyQualifiedName}`);
        shortNamePrefix.append(`Function ${symbol.name}`);

        const postfix = new StringBuilder();

        if (symbol.isGlobal) {
            postfix.append(' Global');
        }

        if (symbol.isNative) {
            postfix.append(' Native');
        }

        if (symbol.userFlags.length > 0) {
            postfix.append(' ');
            postfix.append(symbol.userFlags.join(' '));
        }

        return {
            kind: 'function',
            prefix: prefix.toString(),
            shortNamePrefix: shortNamePrefix.toString(),
            parameters: symbol.parameters.map((p) =>
                this.getDisplayTextForParameter(p)
            ),
            postfix: postfix.toString(),
            documentation: symbol.documentation,
        };
    }

    private getDisplayTextForVariable(symbol: VariableSymbol) {
        const sb = new StringBuilder();

        const valueType = this._program.getTypeForName(symbol.valueType.name);
        sb.append(
            `${
                valueType ? valueType.fullyQualifiedName : symbol.valueType.name
            } `
        );

        sb.append(
            symbol.declaration.node.kind === NodeKind.DeclareStatement
                ? symbol.name
                : symbol.fullyQualifiedName
        );

        if (
            symbol.declaration.node.initialValue &&
            isLiteral(symbol.declaration.node.initialValue)
        ) {
            sb.append(` = ${symbol.declaration.node.initialValue.value}`);
        }

        if (symbol.isConst) {
            sb.append(' IsConst');
        }

        return {
            kind: 'variable',
            text: sb.toString(),
        };
    }

    private getDisplayTextForParameter(symbol: ParameterSymbol) {
        const sb = new StringBuilder();

        const valueType = this._program.getTypeForName(symbol.valueType.name);
        sb.append(
            `${
                valueType ? valueType.fullyQualifiedName : symbol.valueType.name
            } `
        );

        sb.append(symbol.name);

        if (symbol.declaration.node.defaultValue) {
            sb.append(` = ${symbol.declaration.node.defaultValue.value}`);
        }

        return {
            kind: 'parameter',
            text: sb.toString(),
        };
    }

    private getDisplayTextForProperty(symbol: PropertySymbol) {
        const sb = new StringBuilder();

        const valueType = this._program.getTypeForName(symbol.valueType.name);
        sb.append(
            `${
                valueType ? valueType.fullyQualifiedName : symbol.valueType.name
            } `
        );

        sb.append(`Property ${symbol.fullyQualifiedName}`);

        if (symbol.declaration.node.initialValue) {
            sb.append(` = ${symbol.declaration.node.initialValue.value}`);
        }

        if (symbol.isAutoReadOnly) {
            sb.append(' AutoReadOnly');
        } else if (symbol.isAuto) {
            sb.append(' Auto');
        }

        if (symbol.isConst) {
            sb.append(' Const');
        }

        return {
            kind: 'property',
            text: sb.toString(),
            documentation: symbol.documentation,
        };
    }

    private getDisplayTextForScript(symbol: ScriptSymbol): DisplayText {
        const sb = new StringBuilder();
        sb.append(`Scriptname ${symbol.name}`);

        if (symbol.extendedScript) {
            const scriptType = this._program.getTypeForName(
                symbol.extendedScript.name
            );

            sb.append(
                ` extends ${
                    scriptType ? scriptType.name : symbol.extendedScript.name
                }`
            );
        }

        if (symbol.isConst) {
            sb.append(' Const');
        }

        if (symbol.isNative) {
            sb.append(' Native');
        }

        if (symbol.userFlags.length > 0) {
            sb.append(' ');
            sb.append(symbol.userFlags.join(' '));
        }

        return {
            kind: 'script',
            text: sb.toString(),
            documentation: symbol.documentation,
        };
    }

    private getBasicDisplayText(name: string, kind: string, keyword: string) {
        const sb = new StringBuilder();
        sb.append(`${keyword} ${name}`);

        return {
            kind,
            text: sb.toString(),
        };
    }

    private getDisplayTextForState(symbol: StateSymbol) {
        const sb = new StringBuilder();
        if (symbol.isAuto) {
            sb.append('Auto ');
        }

        sb.append(`State ${symbol.fullyQualifiedName}`);

        return {
            kind: 'state',
            text: sb.toString(),
        };
    }
}
