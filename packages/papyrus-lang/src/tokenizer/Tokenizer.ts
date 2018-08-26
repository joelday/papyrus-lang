import { Diagnostics } from '../Diagnostics';
import { Token } from './Token';
import { TokenKind } from './TokenKind';

const textToToken = new Map<string, TokenKind>([
    ['{', TokenKind.OpenBraceToken],
    ['}', TokenKind.CloseBraceToken],
    ['(', TokenKind.OpenParenToken],
    [')', TokenKind.CloseParenToken],
    ['[', TokenKind.OpenBracketToken],
    [']', TokenKind.CloseBracketToken],
    ['.', TokenKind.DotToken],
    [';', TokenKind.SemicolonToken],
    [',', TokenKind.CommaToken],
    ['<', TokenKind.LessThanToken],
    ['>', TokenKind.GreaterThanToken],
    ['<=', TokenKind.LessThanEqualsToken],
    ['>=', TokenKind.GreaterThanEqualsToken],
    ['==', TokenKind.EqualsEqualsToken],
    ['!=', TokenKind.ExclamationEqualsToken],
    ['+', TokenKind.PlusToken],
    ['-', TokenKind.MinusToken],
    ['*', TokenKind.AsteriskToken],
    ['/', TokenKind.SlashToken],
    ['%', TokenKind.PercentToken],
    ['++', TokenKind.PlusPlusToken],
    ['--', TokenKind.MinusMinusToken],
    ['!', TokenKind.ExclamationToken],
    ['&&', TokenKind.AmpersandAmpersandToken],
    ['||', TokenKind.BarBarToken],
    ['=', TokenKind.EqualsToken],
    ['+=', TokenKind.PlusEqualsToken],
    ['-=', TokenKind.MinusEqualsToken],
    ['*=', TokenKind.AsteriskEqualsToken],
    ['/=', TokenKind.SlashEqualsToken],
    ['%=', TokenKind.PercentEqualsToken],
    ['\\', TokenKind.BackslashToken],
    ['"', TokenKind.DoubleQuoteToken],
    [';/', TokenKind.SemicolonSlashToken],
    ['/;', TokenKind.SlashSemicolonToken],
    ['[]', TokenKind.ArrayToken],
    ['as', TokenKind.AsKeyword],
    ['auto', TokenKind.AutoKeyword],
    ['autoreadonly', TokenKind.AutoReadOnlyKeyword],
    ['betaonly', TokenKind.BetaOnlyKeyword],
    ['const', TokenKind.ConstKeyword],
    ['customevent', TokenKind.CustomEventKeyword],
    ['debugonly', TokenKind.DebugOnlyKeyword],
    ['else', TokenKind.ElseKeyword],
    ['elseif', TokenKind.ElseIfKeyword],
    ['endevent', TokenKind.EndEventKeyword],
    ['endfunction', TokenKind.EndFunctionKeyword],
    ['endgroup', TokenKind.EndGroupKeyword],
    ['endif', TokenKind.EndIfKeyword],
    ['endproperty', TokenKind.EndPropertyKeyword],
    ['endstate', TokenKind.EndStateKeyword],
    ['endstruct', TokenKind.EndStructKeyword],
    ['endwhile', TokenKind.EndWhileKeyword],
    ['event', TokenKind.EventKeyword],
    ['extends', TokenKind.ExtendsKeyword],
    ['function', TokenKind.FunctionKeyword],
    ['global', TokenKind.GlobalKeyword],
    ['group', TokenKind.GroupKeyword],
    ['if', TokenKind.IfKeyword],
    ['import', TokenKind.ImportKeyword],
    ['is', TokenKind.IsKeyword],
    ['native', TokenKind.NativeKeyword],
    ['new', TokenKind.NewKeyword],
    ['property', TokenKind.PropertyKeyword],
    ['return', TokenKind.ReturnKeyword],
    ['scriptname', TokenKind.ScriptNameKeyword],
    ['state', TokenKind.StateKeyword],
    ['struct', TokenKind.StructKeyword],
    ['while', TokenKind.WhileKeyword],
    ['true', TokenKind.TrueKeyword],
    ['false', TokenKind.FalseKeyword],
    ['none', TokenKind.NoneKeyword],
]);

export function getTextTokenKind(text: string) {
    if (/\r\n|\r|\n/g.test(text)) {
        return TokenKind.NewLineTrivia;
    }

    if (/\s+/g.test(text)) {
        return TokenKind.WhitespaceTrivia;
    }

    const kind = textToToken.get(text.toLowerCase());

    if (kind === undefined) {
        if (/0[xX][0-9a-fA-F]+/g.test(text)) {
            return TokenKind.HexLiteral;
        }

        if (/[a-zA-Z_]+[:a-zA-Z_0-9]*/g.test(text)) {
            return TokenKind.Identifier;
        }

        if (/[0-9]+\.[0-9]+/g.test(text)) {
            return TokenKind.FloatLiteral;
        }

        if (/[0-9]+/g.test(text)) {
            return TokenKind.IntLiteral;
        }
    }

    return kind || TokenKind.Unknown;
}

function escapeRegExp(text: string) {
    return text.replace(/[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g, '\\$&');
}

const regexEscapedTokenTexts = Array.from(textToToken.keys())
    .sort((x, y) => y.length - x.length)
    .map(escapeRegExp);

const tokenElementRegex = new RegExp(
    `(\\r\\n|\\s+|[a-zA-Z_]+[:a-zA-Z_0-9]*|0[xX][0-9a-fA-F]+|[0-9]+\\.[0-9]+|${regexEscapedTokenTexts.join(
        '|'
    )})`,
    'ig'
);

function splitTextIntoTokenElements(text: string) {
    return text
        .split(tokenElementRegex)
        .filter((t) => t !== undefined && t !== '');
}

export class Tokenizer {
    public *tokenize(
        sourceText: string,
        diagnostics: Diagnostics
    ): IterableIterator<Token> {
        const tokenElements = splitTextIntoTokenElements(sourceText);

        let inDocumentationComment = false;
        let inMultilineComment = false;
        let inSingleLineComment = false;
        let inStringLiteral = false;
        let stringLiteralStartPosition = -1;

        let tokenIndex = 0;
        let text = tokenElements[tokenIndex];
        let previousText = '';
        let pos = 0;

        let previousToken: Token;

        while (tokenIndex < tokenElements.length) {
            let nextPosition = pos + text.length;
            let kind = getTextTokenKind(text);

            if (inDocumentationComment) {
                if (kind === TokenKind.CloseBraceToken) {
                    inDocumentationComment = false;
                } else {
                    kind = TokenKind.DocumentationCommentTrivia;
                }
            } else if (inMultilineComment) {
                if (kind === TokenKind.SlashSemicolonToken) {
                    inMultilineComment = false;
                } else {
                    kind = TokenKind.MultilineCommentTrivia;
                }
            } else if (inSingleLineComment) {
                if (kind === TokenKind.NewLineTrivia) {
                    inSingleLineComment = false;
                } else {
                    kind = TokenKind.SingleLineCommentTrivia;
                }
            } else if (inStringLiteral) {
                if (kind === TokenKind.NewLineTrivia) {
                    inStringLiteral = false;
                    diagnostics.addError('Unterminated string literal.', {
                        start: stringLiteralStartPosition,
                        end: pos,
                    });
                } else if (
                    kind === TokenKind.BackslashToken &&
                    getTextTokenKind(previousText) !== TokenKind.BackslashToken
                ) {
                    // we know we are escaping something
                    kind = TokenKind.StringLiteralContent;

                    tokenIndex++;
                    /* istanbul ignore next */
                    if (tokenIndex === tokenElements.length) {
                        /* istanbul ignore next */
                        break;
                    }

                    const nextText = tokenElements[tokenIndex];
                    text = text + nextText;
                    nextPosition = nextPosition + nextText.length;
                } else if (kind === TokenKind.DoubleQuoteToken) {
                    inStringLiteral = false;

                    yield (previousToken = new Token(
                        kind,
                        text,
                        { start: pos, end: nextPosition },
                        previousToken
                    ));

                    previousText = text;
                    pos = nextPosition;

                    tokenIndex++;
                    /* istanbul ignore next */
                    if (tokenIndex === tokenElements.length) {
                        /* istanbul ignore next */
                        break;
                    }

                    text = tokenElements[tokenIndex];

                    continue;
                } else {
                    kind = TokenKind.StringLiteralContent;
                }
            }

            if (
                kind === TokenKind.OpenBraceToken &&
                !inSingleLineComment &&
                !inMultilineComment &&
                !inStringLiteral
            ) {
                inDocumentationComment = true;
            } else if (
                kind === TokenKind.SemicolonSlashToken &&
                !inSingleLineComment &&
                !inStringLiteral
            ) {
                inMultilineComment = true;
            } else if (
                kind === TokenKind.SemicolonToken &&
                !inDocumentationComment &&
                !inMultilineComment &&
                !inStringLiteral
            ) {
                inSingleLineComment = true;
            } else if (
                kind === TokenKind.DoubleQuoteToken &&
                !inDocumentationComment &&
                !inMultilineComment &&
                !inSingleLineComment &&
                !inStringLiteral
            ) {
                inStringLiteral = true;
                stringLiteralStartPosition = pos;
            }

            yield (previousToken = new Token(
                kind,
                text,
                { start: pos, end: nextPosition },
                previousToken
            ));
            previousText = text;
            pos = nextPosition;

            tokenIndex++;
            if (tokenIndex === tokenElements.length) {
                break;
            }

            text = tokenElements[tokenIndex];
        }

        /* istanbul ignore next */
        if (inStringLiteral) {
            /* istanbul ignore next */
            diagnostics.addError('Unterminated string literal.', {
                start: stringLiteralStartPosition,
                end: sourceText.length,
            });
        }

        yield new Token(
            TokenKind.EndOfFileToken,
            '',
            { start: sourceText.length, end: sourceText.length },
            previousToken
        );
    }
}
