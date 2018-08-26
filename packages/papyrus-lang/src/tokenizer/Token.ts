import { Range } from '../common/Range';
import { TokenKind } from './TokenKind';

export class Token {
    private readonly _kind: TokenKind;
    private readonly _text: string;
    private readonly _range: Range;
    private readonly _previous: Token;
    private _next: Token;

    constructor(kind: TokenKind, text: string, range: Range, previous: Token) {
        this._kind = kind;
        this._text = text;
        this._range = range;
        this._previous = previous;

        if (this._previous) {
            this._previous._next = this;
        }
    }

    get kind() {
        return this._kind;
    }

    get text() {
        return this._text;
    }

    get range() {
        return this._range;
    }

    get previous() {
        return this._previous;
    }

    get next() {
        return this._next;
    }

    get isWhitespace() {
        return (
            this._kind === TokenKind.WhitespaceTrivia ||
            this._kind === TokenKind.NewLineTrivia
        );
    }

    get isLineContinuation() {
        return this._kind === TokenKind.BackslashToken;
    }

    get isPossibleLiteral() {
        return (
            this._kind === TokenKind.MinusToken ||
            this._kind === TokenKind.IntLiteral ||
            this._kind === TokenKind.FloatLiteral ||
            this._kind === TokenKind.HexLiteral ||
            this._kind === TokenKind.TrueKeyword ||
            this._kind === TokenKind.FalseKeyword ||
            this._kind === TokenKind.DoubleQuoteToken ||
            this._kind === TokenKind.NoneKeyword
        );
    }

    get isDocComment() {
        return (
            this._kind === TokenKind.DocumentationCommentTrivia ||
            this._kind === TokenKind.OpenBraceToken ||
            this._kind === TokenKind.CloseBraceToken
        );
    }

    get isComment() {
        return (
            this._kind === TokenKind.SingleLineCommentTrivia ||
            this._kind === TokenKind.MultilineCommentTrivia ||
            this._kind === TokenKind.DocumentationCommentTrivia ||
            this._kind === TokenKind.SemicolonToken ||
            this._kind === TokenKind.SlashSemicolonToken ||
            this._kind === TokenKind.SemicolonSlashToken ||
            this._kind === TokenKind.OpenBraceToken ||
            this._kind === TokenKind.CloseBraceToken
        );
    }

    get isFlag() {
        return (
            this._kind === TokenKind.AutoKeyword ||
            this._kind === TokenKind.AutoReadOnlyKeyword ||
            this._kind === TokenKind.BetaOnlyKeyword ||
            this._kind === TokenKind.ConstKeyword ||
            this._kind === TokenKind.DebugOnlyKeyword ||
            this._kind === TokenKind.GlobalKeyword ||
            this._kind === TokenKind.NativeKeyword
        );
    }

    get isAssignmentOperator() {
        return (
            this._kind === TokenKind.EqualsToken ||
            this._kind === TokenKind.PlusEqualsToken ||
            this._kind === TokenKind.MinusEqualsToken ||
            this._kind === TokenKind.SlashEqualsToken ||
            this._kind === TokenKind.AsteriskEqualsToken ||
            this._kind === TokenKind.PercentEqualsToken
        );
    }

    get isComparisonOperator() {
        return (
            this._kind === TokenKind.EqualsEqualsToken ||
            this._kind === TokenKind.ExclamationEqualsToken ||
            this._kind === TokenKind.GreaterThanToken ||
            this._kind === TokenKind.GreaterThanEqualsToken ||
            this._kind === TokenKind.LessThanToken ||
            this._kind === TokenKind.LessThanEqualsToken
        );
    }

    get isEndOfLogicalLine() {
        return (
            (this._kind === TokenKind.NewLineTrivia &&
                (!this._previous ||
                    (this._previous.kind !== TokenKind.BackslashToken &&
                        this._previous.kind !==
                            TokenKind.MultilineCommentTrivia &&
                        this._previous.kind !==
                            TokenKind.DocumentationCommentTrivia))) ||
            this._kind === TokenKind.EndOfFileToken ||
            this._kind === TokenKind.Unknown
        );
    }

    get isTrivia() {
        return (
            (this.isWhitespace || this.isComment || this.isLineContinuation) &&
            !this.isEndOfLogicalLine
        );
    }

    public toString() {
        return `[Token]: Kind=${TokenKind[this.kind]}, Text='${
            this.text
        }', Range=[${this.range.start}, ${this.range.end}]`;
    }
}
