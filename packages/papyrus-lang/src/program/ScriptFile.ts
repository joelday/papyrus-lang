import { Lazy, LazyTokenEqualityMemoized } from '../common/Lazy';
import { Diagnostics } from '../Diagnostics';
import { ScriptNode } from '../parser/Node';
import { Parser } from '../parser/Parser';
import { SymbolBinder } from '../symbols/SymbolBinder';
import { Token } from '../tokenizer/Token';
import { Tokenizer } from '../tokenizer/Tokenizer';
import { getTypeOfScript, ScriptType, Type } from '../types/Type';
import { LanguageServiceHost, ScriptText } from './LanguageServiceHost';
import { Program } from './Program';
import { ScriptReferenceValue } from './ScriptReferenceValue';

export interface ScriptNodeResult {
    readonly scriptNode: ScriptNode;
    readonly diagnostics: Diagnostics;
}

export interface TokensResult {
    readonly tokens: Iterable<Token>;
    readonly diagnostics: Diagnostics;
}

export class ScriptFile {
    private readonly _scriptName: string;
    private readonly _uri: string;
    private readonly _program: Program;
    private readonly _languageServiceHost: LanguageServiceHost;
    private readonly _tokens: Lazy<TokensResult>;
    private readonly _type: LazyTokenEqualityMemoized<ScriptType>;
    private readonly _scriptNode: LazyTokenEqualityMemoized<ScriptNodeResult>;
    private _text: ScriptText;

    public constructor(
        scriptName: string,
        uri: string,
        program: Program,
        languageServiceHost: LanguageServiceHost
    ) {
        this._scriptName = scriptName;
        this._uri = uri;
        this._program = program;
        this._languageServiceHost = languageServiceHost;

        this._tokens = new Lazy(
            () => {
                this._text = this._languageServiceHost.getScriptText(this._uri);

                if (this._text === null) {
                    return null;
                }

                const diagnostics = new Diagnostics(this._uri, this._text.text);

                const tokenizer = new Tokenizer();
                const tokens = Array.from(
                    tokenizer.tokenize(this._text.text, diagnostics)
                );

                return { tokens, diagnostics };
            },
            () =>
                this._text
                    ? this._languageServiceHost.getScriptVersion(this._uri) !==
                      this._text.version
                    : null
        );

        this._scriptNode = new LazyTokenEqualityMemoized(
            () => {
                const { tokens, diagnostics } = this._tokens.value;

                const parser = new Parser();
                const scriptNode = parser.parse(tokens, diagnostics);
                if (scriptNode.header.identifier.name === '') {
                    return { scriptNode: null, diagnostics };
                }

                scriptNode.scriptFile = this;

                const binder = new SymbolBinder();
                binder.bindSymbols(scriptNode, diagnostics);

                return { scriptNode, diagnostics };
            },
            () => this._tokens.value.tokens
        );

        this._type = new LazyTokenEqualityMemoized(
            () => getTypeOfScript(this.scriptNode.scriptNode),
            () => this.scriptNode
        );
    }

    public validateTypesAndReferences() {
        const { scriptNode } = this._scriptNode.value;
        const diagnostics = new Diagnostics(this._uri, this._text.text);

        if (scriptNode) {
            this._program.typeChecker.checkTypesAndReferences(
                scriptNode,
                diagnostics
            );
        }

        return diagnostics;
    }

    public maybeCreateVersionDerivedValue<T>(
        resolver: (file: ScriptFile) => T
    ) {
        if (resolver(this)) {
            return this.createVersionDerivedValue(resolver);
        }

        return null;
    }

    public createVersionDerivedValue<T>(resolver: (file: ScriptFile) => T) {
        return new ScriptReferenceValue(
            this._program,
            this._scriptName,
            resolver
        );
    }

    public get scriptNode() {
        return this._scriptNode.value;
    }

    public get tokens() {
        return this._tokens.value;
    }

    public get type() {
        return this._type.value;
    }

    public get program() {
        return this._program;
    }

    public get scriptName() {
        return this._scriptName;
    }

    public get text() {
        return this._text ? this._text.text : null;
    }

    public get version() {
        return this._text ? this._text.version : null;
    }

    public get uri() {
        return this._uri;
    }
}
