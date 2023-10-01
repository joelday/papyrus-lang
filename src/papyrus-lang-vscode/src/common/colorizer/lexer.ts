import { Token, TokenType } from './types';

interface TokenRegexMap {
    regex: RegExp;
    tokenType: TokenType;
}

const tokenTypes: TokenRegexMap[] = [
    { regex: /^\s+/, tokenType: 'WHITESPACE' },
    { regex: /^[{}]/, tokenType: 'BRACE' },
    { regex: /^[[\]]/, tokenType: 'BRACKET' },
    { regex: /^:/, tokenType: 'COLON' },
    { regex: /^,/, tokenType: 'COMMA' },
    { regex: /^-?\d+(?:\.\d+)?(?:e[+-]?\d+)?/i, tokenType: 'NUMBER_LITERAL' },
    { regex: /^"(?:\\.|[^"\\])*"(?=\s*:)/, tokenType: 'STRING_KEY' },
    { regex: /^"(?:\\.|[^"\\])*"/, tokenType: 'STRING_LITERAL' },
    { regex: /^true|^false/, tokenType: 'BOOLEAN_LITERAL' },
    { regex: /^null/, tokenType: 'NULL_LITERAL' },
];

export function getTokens(json: string | object, options: { pretty?: boolean } = {}): Token[] {
    let input: string;

    if (options.pretty) {
        const inputObj = typeof json === 'string' ? JSON.parse(json) : json;
        input = JSON.stringify(inputObj, null, 2);
    } else {
        input = typeof json === 'string' ? json : JSON.stringify(json);
    }

    const tokens: Token[] = [];
    let foundToken: boolean;

    do {
        foundToken = false;
        for (let i = 0; i < tokenTypes.length; i++) {
            const match = tokenTypes[i].regex.exec(input);
            if (match) {
                tokens.push({
                    type: tokenTypes[i].tokenType,
                    value: match[0],
                });
                input = input.substring(match[0].length);
                foundToken = true;
                break;
            }
        }
    } while (_allTokensAnalyzed(input, foundToken));

    return tokens;
}

/**
 * @author Willian Magalhães Gonçalves
 * @description Are all tokens analyzed?
 * @param {*} input - Input
 * @param {*} foundToken - Found token
 * @returns {boolean} checkResult - Check result
 * @private
 */
function _allTokensAnalyzed(input: string, foundToken: boolean): boolean {
    const safeInput = input || '';

    const inputLength = safeInput.length;
    return inputLength > 0 && foundToken;
}
